# 小乔点击工具

一款 Windows 桌面自动点击工具，用于按指定坐标、范围、时间间隔和次数执行自动点击任务。

当前长期维护主线为 WPF 版本，技术栈为 C# / .NET / WPF。

## 项目目录

```text
wpf/
  XiaoQiaoClickTool/      WPF 主程序源码
  README.md              WPF 版本详细说明
  TEST_RECORD.md         测试记录
  installer-wpf.iss      Inno Setup 安装包脚本
logo图标.png             项目图标原图
```

## 开发运行

进入项目根目录后执行：

```powershell
dotnet run --project .\wpf\XiaoQiaoClickTool\XiaoQiaoClickTool.csproj
```

## 已实现功能

- 选择点击坐标并显示当前坐标
- 圆形范围 / 矩形范围点击
- 固定时间 / 随机时间点击
- 时间限制 / 次数限制 / 手动停止
- 后台点击循环，不阻塞界面
- 失败重试、失败率提示和连续失败警告
- 1000 次及以上任务启动前提醒防休眠、锁屏和远程断开
- 暂停、继续、停止状态管理
- 3 秒取点和鼠标跟随 Overlay 预览
- 多屏幕和不同 DPI 缩放适配
- 配置、日志和历史记录本地保存
- 全局快捷键：F6 开始/继续，F7 暂停，F8 停止
- 高级设置窗口、完成提示音和确认框
- 图标已接入 exe 和安装包
- Inno Setup 标准安装包脚本

## 发布说明

构建产物、安装包和本地工具配置不会提交到 GitHub，避免仓库过大。

如需查看 WPF 版本更详细说明，请阅读：

```text
wpf/README.md
```
