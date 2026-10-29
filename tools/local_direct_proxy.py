"""极简本地 HTTP 代理（直连，无上游）：监听 127.0.0.1:10808，支持 CONNECT 隧道与普通 GET/POST 转发。
用途：现场代理软件(v2rayN)未启动但目标站点可直连时，给 git / 浏览器提供一个可用的代理端口。
仅本机使用，不做任何缓存或改写。
"""
import socket
import threading
import sys

LISTEN = ("127.0.0.1", 10808)
BUF = 65536


def pipe(a, b):
    try:
        while True:
            data = a.recv(BUF)
            if not data:
                break
            b.sendall(data)
    except Exception:
        pass
    finally:
        try:
            b.shutdown(socket.SHUT_WR)
        except Exception:
            pass


def handle(client):
    try:
        client.settimeout(15)
        head = b""
        while b"\r\n\r\n" not in head:
            chunk = client.recv(4096)
            if not chunk:
                client.close()
                return
            head += chunk
            if len(head) > 65536:
                break
        first, _, rest = head.partition(b"\r\n")
        parts = first.split()
        if len(parts) < 2:
            client.close()
            return
        method, target = parts[0].decode("latin-1"), parts[1].decode("latin-1")

        if method.upper() == "CONNECT":
            host, _, port = target.partition(":")
            upstream = socket.create_connection((host, int(port or 443)), timeout=15)
            client.sendall(b"HTTP/1.1 200 Connection Established\r\n\r\n")
        else:
            if target.startswith("http://"):
                target = target[7:]
            hostport, _, path = target.partition("/")
            host, _, port = hostport.partition(":")
            upstream = socket.create_connection((host, int(port or 80)), timeout=15)
            rest = rest  # 已包含 body 起始部分
            upstream.sendall((method + " /" + path + " HTTP/1.1\r\n").encode("latin-1") +
                             rest.split(b"\r\n", 1)[1] if b"\r\n" in rest else b"")

        upstream.settimeout(None)
        client.settimeout(None)
        t1 = threading.Thread(target=pipe, args=(client, upstream), daemon=True)
        t2 = threading.Thread(target=pipe, args=(upstream, client), daemon=True)
        t1.start()
        t2.start()
        t1.join()
        t2.join()
        upstream.close()
        client.close()
    except Exception:
        try:
            client.close()
        except Exception:
            pass


def main():
    srv = socket.socket()
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(LISTEN)
    srv.listen(64)
    print("local direct proxy listening on %s:%d" % LISTEN, flush=True)
    while True:
        c, _ = srv.accept()
        threading.Thread(target=handle, args=(c,), daemon=True).start()


if __name__ == "__main__":
    main()
