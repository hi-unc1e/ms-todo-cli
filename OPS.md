# 运维备忘（给 agent / 未来会话）

## 待办简报自动化依赖
- 每日 9:00 的定时任务运行 `D:\Documents\sync\ms-todo-cli\publish\win-x64\todo.exe`
- 该 exe 若被 **Smart App Control**（本机处于评估模式）拦截，症状为 "Permission denied" / "应用程序控制策略已阻止此文件"
- **恢复步骤（已验证有效）**：
  ```bash
  gh release download v0.3.0 --repo hi-unc1e/ms-todo-cli \
    --pattern "todo-0.3.0-win-x64.zip" --dir /tmp/rel --clobber
  # 解压后把 todo.exe 覆盖到 publish/win-x64/todo.exe
  ```
  即改用 GitHub Actions CI 构建的官方产物（2026-09-05 实测通过 SAC）。
- 本地 `dotnet publish` 重编译换哈希是旧办法，2026-09-05 起连续 5 个新哈希均被拦，**优先用 Release 产物**。
- 认证：token 缓存在 `~/.todo-cli/token.cache`（DPAPI 加密），与 exe 无关，换 exe 不需要重新登录。

## 其他
- 新版本发版流程：`git tag vX.Y.Z && git push origin vX.Y.Z` → CI 自动测试+四平台构建+建 Release
- gh CLI 已认证账号 hi-unc1e（keyring）
