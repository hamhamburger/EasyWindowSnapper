# EasyWindowSnapper

[![Open in Visual Studio Code](https://img.shields.io/static/v1?logo=visualstudiocode&label=&message=Open%20in%20Visual%20Studio%20Code&labelColor=2c2c32&color=007acc&logoColor=007acc)](https://open.vscode.dev/hamhamburger/EasyWindowSnapper)

Best Window Manager for Keyboard Bunglers and Mouse Junkies:　　

Mouse-oriented alternative to AltTab, allowing for effortless snapping of windows to the left or right

# Download
[https://github.com/hamhamburger/EasyWindowSnapper/releases](https://github.com/hamhamburger/EasyWindowSnapper/releases)



# How to Use


| Control | With Back Button | With Forward Button |
|---|---|---|
| Wheel | Adjust window ratio | Select window |
| Left-click | Snap left | Snap selected window left |
| Right-click | Snap right | Snap selected window right |
| Middle-click (default) | Swap left-right windows | Close selected window |
| Middle-click (other options) | Maximize, minimize, or close window under cursor | Maximize, minimize, or close selected window |


## Demo

Snap window under cursor by click and adjust ratio with mouse wheel:
![Window Snap Demo](assets/demoSnap.gif)

Snap window directly from window list and close window with middle-click:
![AltTab Alternative Demo](assets/demoAltTab.gif)

## Configuration
Right-click the blue monitor icon in the task tray for settings and to exit.

### Performance Note
Apps like Explorer, video editors, and games may slow when resizing windows. A warning sound may play when memory is low.

# Known Issues

- Rarely, the Forward/Back button may stick when repeatedly adjusting the window ratio. Pressing the stuck button fixes it.

- Clicks may become unresponsive, a bug from previous versions. Press Ctrl+Alt+Delete to resolve.

Those issues are caused by sluggish resizing of applications like Explorer.  
Can happen in lower-specification computer.
Currently implementing solutions for these problems.


# Note
While the application is running, it substitutes the mouse's Forward and Back buttons with the Browser Back and Forward functions of the keyboard.  
These buttons will work as intended when pressed alone.  
However, in VSCode, Browser Back and Forward not work by default. Please add  Browser Back and Browser Forward key to the Go Forward and Go Back shortcuts in VSCode.
