# Usage

```powershell
Cake.exe [script] [--verbosity=value]
         [--showdescription] [--dryrun] [..]
```

# Options

| Option                | Description                                                  |
|-----------------------|--------------------------------------------------------------|
| --verbosity=value     | Specifies the amount of information to be displayed.<br>(Quiet, Minimal, Normal, Verbose, Diagnostic) |
| --debug               | Performs a debug.                                            |
| --showdescription     | Shows description about tasks.                               |
| --dryrun              | Performs a dry run.                                          |
| --exclusive           | Execute a single task without any dependencies.              |
| --bootstrap           | Download/install modules defined by #module directives       |
| --version             | Displays version information.                                |
| --help                | Displays usage information.                                  |

# Examples

```powershell
Cake.exe
```

```powershell
Cake.exe build.cake --verbosity=quiet
```

```powershell
Cake.exe build.cake --showdescription
```
