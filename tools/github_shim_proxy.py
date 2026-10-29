"""GitHub 直连穿透代理（本机 10808）

背景：本机 DNS 把 github.com 解析到被墙的 IP（20.205.243.x），但同域名其他 IP 可达。
做法：HTTP CONNECT 隧道里只负责 TCP 连接，TLS 由客户端端到端完成（SNI/Host 仍是 github.com，
证书校验正常），所以只要把连接目标换成可达 IP 即可，无需改 hosts、无需管理员权限。

用法：
    python github_shim_proxy.py            # 监听 127.0.0.1:10808
    git -c http.proxy=http://127.0.0.1:10808 push ...
"""
import socket
import threading

LISTEN = ("127.0.0.1", 10808)
BUF = 65536

# 域名 -> 可达 IP（被墙的 20.205.243.166 不用）
OVERRIDES = {
    "github.com": "140.82.112.3",
    "www.github.com": "140.82.112.3",
    "gist.github.com": "140.82.112.3",
}


def pick_ip(host, port):
    ip = OVERRIDES.get(host.lower())
    if ip:
        print("  override %s -> %s" % (host, ip), flush=True)
        return ip, port
    return host, port


def pipe(src, dst):
    try:
        while True:
            data = src.recv(BUF)
            if not data:
                break
            dst.sendall(data)
    except Exception:
        pass
    finally:
        for s in (dst, src):
            try:
                s.shutdown(socket.SHUT_RDWR)
            except Exception:
                pass


def handle(client):
    upstream = None
    try:
        client.settimeout(20)
        head = b""
        while b"\r\n\r\n" not in head:
            chunk = client.recv(4096)
            if not chunk:
                client.close()
                return
            head += chunk
            if len(head) > 65536:
                break

        lines = head.split(b"\r\n")
        req = lines[0].split()
        if len(req) < 2:
            client.close()
            return
        method = req[0].decode("latin-1").upper()
        target = req[1].decode("latin-1")

        if method == "CONNECT":
            host, _, port = target.partition(":")
            host = host.strip("[]")
            port = int(port or 443)
            ip, port = pick_ip(host, port)
            upstream = socket.create_connection((ip, port), timeout=15)
            client.sendall(b"HTTP/1.1 200 Connection Established\r\n\r\n")
        else:
            url = target[7:] if target.startswith("http://") else target
            hostport, _, path = url.partition("/")
            host, _, port = hostport.partition(":")
            port = int(port or 80)
            ip, port = pick_ip(host, port)
            upstream = socket.create_connection((ip, port), timeout=15)
            rebuilt = [lines[0].decode("latin-1")]
            for ln in lines[1:]:
                if ln:
                    rebuilt.append(ln.decode("latin-1"))
            upstream.sendall(("\r\n".join(rebuilt) + "\r\n\r\n").encode("latin-1"))
            rest = b"\r\n\r\n".join(head.split(b"\r\n\r\n")[1:])
            if rest:
                upstream.sendall(rest)

        client.settimeout(None)
        upstream.settimeout(None)
        t1 = threading.Thread(target=pipe, args=(client, upstream), daemon=True)
        t2 = threading.Thread(target=pipe, args=(upstream, client), daemon=True)
        t1.start()
        t2.start()
        t1.join()
        t2.join()
    except Exception as exc:
        print("  conn error:", type(exc).__name__, exc, flush=True)
    finally:
        for s in (upstream, client):
            try:
                s.close()
            except Exception:
                pass


def main():
    srv = socket.socket()
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(LISTEN)
    srv.listen(128)
    print("github shim proxy on %s:%d (overrides: %s)" % (LISTEN[0], LISTEN[1], OVERRIDES), flush=True)
    while True:
        c, addr = srv.accept()
        threading.Thread(target=handle, args=(c,), daemon=True).start()


if __name__ == "__main__":
    main()
