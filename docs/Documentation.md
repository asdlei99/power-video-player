# Release history:

**v2.1.5212**
04/09/2014

9155 - Fix file association in Windows 8
9251 - Support audio stream switching when a source/splitter filter implementes IAMStreamSelect
9252 - Support subtitles if a source/splitter stream provides an appropriate stream
9265 - Properly report present audio streams when a source/splitter filter implementes IAMStreamSelect

**v2.0.4960**
07/31/2013

8918 - Can't play DVD when LAV filters are installed

**v2.0.4771**
01/23/2013

5520 - WPF re-implementation of the player (+ a bunch of rethinking and refactoring of the core)
4852 - Drag and Drop support
4855 - More aspect ratio's

**v1.2.3902**
09/07/2010

4856 - Ability to take screenshots (new feature).
5519 - Refactor the core and extract presentation independent logic.
4851 - If video cannot be rendered the whole file is not played.
4905 - About dialog doesn't scale properly.
4452 - Cursor keeps showing up in fullscreen mode even if mouse does not move
+ a few minor fixes.

**v1.1.3776**
05/04/2010

- mainly a rebuild under Ms-PL license, no functional changes compared to v1.1.3658

**v1.1.3658**
01/06/2010

- added a possibility to choose a desired video renderer when playing DVD
- fixed aspect ratio issues when playing DVD
- added support for Vista/7 file association mechanism (Default Programs)

**v1.0.3632**
12/11/2009

- added support for EVR
- fixed VMR/VMR9 issues under Vista in windowed mode (proper configuration)
- fixed VMR/VMR9 aspect ratio issues
- improved automatic graph building
- run on 64bit as well as on 32bit systems

**v0.9**
08/20/2009

- first time publicly available


# Developer info

[Overall design (applies to v1.x)](http://www.dzimchuk.net/blog/post/PVP-on-the-way-to-WPF.aspx)

[Assembly signing](http://www.dzimchuk.net/blog/post/Assembly-signing-in-an-open-source-project.aspx)

