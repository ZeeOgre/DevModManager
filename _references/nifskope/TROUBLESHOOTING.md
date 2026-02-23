### Invisible Starfield meshes, or missing textures on models from other games

This issue is most often caused by incorrect resource settings. Make sure that the game is enabled (Options/Settings.../Resources/Games), and that data paths are set up for it on the Paths tab. Any number of archives and/or folders can be added as paths, and folders are loaded recursively, so the simplest configuration consists of just the data path of the game (e.g. K:/SteamLibrary/steamapps/common/Starfield/Data). If the same resource can be found under multiple paths, then the one listed first has the highest precedence.

### Starfield shapes have no mesh paths

By default, Starfield models are automatically converted to use internal geometry data, which is currently required by most of the mesh editing functionality and spells in NifSkope. This conversion can be disabled in the general settings under NIF, and meshes can be converted to either format (internal or external) with spells.

### Empty or black viewport

This problem is usually related to the OpenGL driver or settings. On systems with more than one GPU, make sure that NifSkope is using the correct one, setting it for the application on the control panel of the driver if necessary. In some cases, an empty viewport can also be caused by using a higher MSAA setting than the maximum supported by the hardware or driver.

### Errors launching NifSkope on Windows 7 or 8

Only the builds using Qt 5 run on Windows versions older than 10, and the [Universal C Runtime](https://support.microsoft.com/en-us/topic/update-for-universal-c-runtime-in-windows-c0514201-7fe6-95a3-b0a5-287930f3560c) should be installed.

### Very long load times

Initializing the asset database can potentially take a long time if the number of loose resource files is unusually large. On Windows, performance is impacted much more by the number of resource folders than the number of files, so the problem is most often caused by having all Starfield geometry data (over 350,000 folders, each containing one .mesh file) added as loose resources. In this case, if the geometries are under the main game data path, it is recommended to add archives manually under Options/Settings.../Resources/Paths, and additional data folders only when they are really needed, instead of the simple configuration that recursively loads all data. Note that the 'Add Archive or File' button allows selecting multiple files at once, and that it is allowed to add sub-folders like K:/SteamLibrary/steamapps/common/Starfield/Data/geometries/MyMod as data paths.

Using too high quality settings for image based lighting can also result in long processing times when the environment map is first loaded.

### Rendering issues on Linux

Running NifSkope under Wayland on Linux may require setting the QT\_QPA\_PLATFORM environment variable to "xcb":

    QT_QPA_PLATFORM=xcb ./NifSkope

