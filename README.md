# Peeklet

Peeklet 是一个 Windows 文件快速预览工具。

它会常驻系统托盘，让你在资源管理器里选中文件后，按一次空格就能快速查看内容，不必反复打开应用。

## 适合用来做什么

- 快速看图片、文本和常见文档内容
- 在文件列表里连续浏览前后文件
- 减少“打开文件再关闭”的打断感

## 如何使用

1. 启动 Peeklet。
2. 在 Windows 资源管理器中选中一个文件。
3. 按空格打开或关闭预览。
4. 预览打开后，按左右方向键切换上一个或下一个文件。
5. 按 Esc 关闭预览。

Peeklet 运行时会驻留在系统托盘中，也可以通过托盘图标再次打开预览或退出程序。

## 支持的预览类型

- 图片：jpg、jpeg、png、bmp、webp、gif、ico、tif、tiff
- 文本和代码：txt、log、json、xml、csv、yml、yaml、ini，以及常见代码文件
- Markdown：md、markdown
- PDF
- SVG
- Office 文档：doc、docx、xls、xlsx、ppt、pptx
- 其他已在 Windows 中注册预览处理器的文件类型

## 使用要求

- Windows 10 或 Windows 11
- 建议使用较新的 Edge WebView2 Runtime，以获得更稳定的 PDF、Markdown 和 SVG 预览体验

## 当前说明

- 目前主要面向 Windows 资源管理器中的文件预览
- 某些文件类型能否预览，取决于你的系统是否已经安装对应的 Windows 预览组件
- 这是一个仍在持续改进中的版本，兼容性和体验会继续优化

## 获取与启动

如果你是普通用户，直接使用发布包中的 Peeklet.exe 启动即可。

首次运行后，程序会进入系统托盘并在后台待命。