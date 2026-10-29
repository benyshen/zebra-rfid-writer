# tools/

开发/部署时用到的小工具，与 RFID 程序本体无关。

## github_shim_proxy.py

**用途**：本机 DNS 把 `github.com` 解析到被墙 IP（如 `20.205.243.x`）、但同域名其他 IP 可达时，
让 `git` 能正常访问 GitHub。不需要改 hosts、不需要管理员权限。

**原理**：起一个本机 HTTP CONNECT 代理，隧道里只负责 TCP 连接；TLS 由客户端端到端完成，
SNI/Host 仍是 `github.com`，证书校验正常，因此只是"把连接目标换成可达 IP"。

```bash
python tools/github_shim_proxy.py            # 监听 127.0.0.1:10808
git -c http.proxy=http://127.0.0.1:10808 ls-remote https://github.com/OWNER/REPO.git
git -c http.proxy=http://127.0.0.1:10808 push origin main
```

可达 IP 在脚本顶部 `OVERRIDES` 里改（默认 `140.82.112.3`）。只监听本机，不对外提供服务。

## push_to_github.bat

**用途**：一键设置远端并推送，代理可选。

```bat
push_to_github.bat https://github.com/benyshen/zebra-rfid-writer.git
push_to_github.bat https://github.com/benyshen/zebra-rfid-writer.git http://127.0.0.1:10809
```

不传代理时直连（`-c http.proxy=` 清空代理，避免被系统里失效的代理配置拖死）。
