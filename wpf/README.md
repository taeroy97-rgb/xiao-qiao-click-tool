# 小乔点击工具 WPF 版

这是新的长期维护主线，技术栈为 C# / .NET 10 / WPF。旧 Python、PySide6、WebView 版本只作为参考，不再作为最终产品主线。

## 运行方式

开发运行：

```powershell
dotnet run --project .\XiaoQiaoClickTool\XiaoQiaoClickTool.csproj
```

Release 发布目录：

```text
wpf\publish\XiaoQiaoClickTool\XiaoQiaoClickTool.exe
```

标准安装包：

```text
安装包\小乔点击工具 1.1.7.exe
```

## 已实现

- 浅色玻璃拟态三列主界面
- 小尺寸“选择点”按钮和独立坐标反馈
- 当前坐标显示
- 圆形范围 / 矩形范围默认 17，可来回切换，数值支持滑块、手动输入和键盘上下键微调
- 圆形范围 / 矩形范围实时反馈监控
- 固定时间 / 随机时间点击
- 时间限制 / 次数限制 / 手动停止
- 后台点击循环，不阻塞 UI 线程
- 点击发送成功后才计数；失败最多重试 3 次，失败不计数但记录发送次数并继续补够目标成功次数
- 连续失败超过 30 次红色警告，失败率偏高时提示，任务不停止
- 1000 次及以上任务启动前显示防休眠/锁屏/远程断开提醒
- 程序默认不请求管理员权限，安装后的桌面图标不显示管理员盾牌；需要点击高权限程序时可右键以管理员身份运行
- 暂停/继续运行中高亮可用，停止后回到“已停止”初始状态
- 3 秒取点和鼠标跟随 Overlay 预览
- 取点 Overlay 按鼠标所在屏幕 DPI 定位，兼容不同显示缩放和多屏环境
- 配置节流保存到 `%AppData%\XiaoQiaoClickTool\settings.json`
- 日志保存到 `%AppData%\XiaoQiaoClickTool\logs\app.log`
- 全局快捷键：F6 开始/继续，F7 暂停，F8 停止
- 独立高级设置窗口：恢复默认、查看配置路径、打开日志目录、最近 10 条记录，已统一玻璃拟态风格并支持滚动避免遮挡
- 完成后提示音和确认框，确认后自动初始化
- 历史记录保存到 `%AppData%\XiaoQiaoClickTool\history.json`
- `.ico` 图标已由根目录 `logo图标.png` 重新生成，并接入 exe 和安装包
- Inno Setup 标准安装包，安装完成默认不自动启动
- 静默安装/卸载验证通过

## 后续优化重点

- 对照目标截图继续做像素级视觉微调
- 完成完整人工压力测试记录
- 如需消除 Windows 未知发布者提示，需要正式代码签名证书
