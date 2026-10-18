# Todo Agent 子系统设计（版本随主线；首版随 v0.5.0 发布，内部修订号见 §7）

> 目标：让 CLI 成为 **Agent → 人类** 的跨平台信息载体与**反馈回路**。
> 原则：纯用 To Do 原生结构（正文/步骤/提醒），不依赖外部服务与外链；不影响既有命令。

## 一、能力边界（2026-10-18 Graph v1.0 实测）

| 能力 | 状态 | 说明 |
|---|---|---|
| HTML 正文（contentType=html） | ✅ | 客户端渲染有限子集：标题/列表/粗体/链接/代码。**表格勿用**，用列表 |
| 子任务 checklistItems | ✅ | 创建时可带；勾选状态 isChecked 可回读 |
| 文件/图片附件 | ❌ | v1.0 POST attachments 对 fileAttachment 报 BadRequest（未开放）；To Do 客户端也会过滤正文 `<img>` |
| 我的一天 / 重复 | ❌ | Graph API 无此能力 |
| Webhook/变更订阅 | ❌ | Graph subscriptions 不支持 todoTask —— **推送不存在，回读只能轮询** |

## 二、表达层（agent → 人）：`todo agent send`

一条"决策请求"任务的解剖：
- **标题**：一句话说清要什么决策（[Agent] 前缀便于人过滤）
- **HTML 正文**：背景、选项对比（列表化）、说明 —— 用 `--html-file`（UTF-8 HTML 文件）或 `--note`（纯文本）
- **步骤 checklistItems**：结构化选项。协议约定：**人勾选某一步 = 对该项确认/投票**；内置信号灯步骤「💬 我在备注里写了意见」表示自由回复
- **提醒 reminder**：把人拉回来看（到点客户端弹通知）
- 反馈区 = 正文本身：**人直接编辑正文追加意见**（To Do 原生操作）

## 三、反馈层（人 → agent）：三通道回读

| 通道 | 检测方式 |
|---|---|
| 人改了正文/备注 | body 归一化后 SHA256 对比 |
| 人勾选/新增了步骤 | (displayName→isChecked) 字典对比 |
| 人完成任务 | status / completedDateTime 对比 |
| 快速预检 | lastModifiedDateTime（避免全量 diff） |

## 四、轮询算法（自适应退避）

```
phase1  决策黄金期   创建后 60 分钟内   每 5 分钟
phase2  常规关注期   之后 24 小时       每 60 分钟
phase3  长尾期       之后               每 24 小时
```
- `todo agent poll`：单次检查（给 cron/自动化用，零常驻）
- `todo agent watch`：内置循环，检出反馈即退出（退出码 0 + JSON diff）；`--timeout` 兜底
- 参数可覆盖：`--p1-interval/--p1-duration/--p2-interval/--p2-duration/--p3-interval`
- 状态持久化 `~/.todo-cli/agent-watch.json`（按 taskId 记 body 哈希/步骤态/相位），跨进程续期

## 五、OOB Callback 的真相与演进

「人点步骤里的链接触发回调」不可行：步骤是纯文本，点击只切换勾选。因此 **勾选动作本身就是 OOB 信号**（原生 UI 一次点击，零新工具）。真正的 URL callback 需要中转服务（外链+外部组件），违背"尽量不外链"，留作 v0.2 演进项。

## 六、机器可读输出

`-o json` 输出：`{taskId, listId, changes:[{type, detail}], bodyNow, checklistNow}` —— 供自动化消费。退出码：`0` 检出反馈 / `1` 无反馈正常结束 / `2` 错误。

## 七、v0.2.0→0.5.0 修订（2026-10-18 review；0.1.0/0.2.0 为合并入主线前的内部迭代号）

架构/体验 review 后的六项修正：
1. **send 即落基线**（修复检测盲区）：创建成功后立刻记录初始快照——send 与首次 poll 之间人给出的反馈不再被当基线吞掉。
2. **store 优先解析**：`poll/watch --task-id` 优先用 watch store 里的 ListId，免去逐列表扫描（N+1 → 1 次 API）。
3. **404 语义化**：任务被人删除时，明确报告"deleted by human"并自动清理 watch 状态。
4. **watch 容错**：单次网络错误不再终结守望；连续 3 次失败才退出（中间 1 分钟重试）。
5. **send 回显守望命令**：成功输出 `todo agent watch --task-id <id>`，操作零记忆负担。
6. **正文预览**：检出 body 变化时打印去标签后的新正文预览（≤200 字）。

另：HTML 正文 >256KB 拒绝、>64KB 警告；版本线 0.2.0。
