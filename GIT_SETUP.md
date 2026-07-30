# Git Setup

This folder is prepared as the public source tree for PZ Reverse Mapper.

## First commit

```powershell
cd path\to\PZReverseMapper-public
git init
git add .
git commit -m "Initial public release"
git branch -M main
git tag v0.1.0
```

## Push to GitHub

```powershell
git remote add origin https://github.com/Unjammer/PZReverseMapper.git
git push -u origin main
git push origin v0.1.0
```

## Build check

```powershell
dotnet build .\PZReverseMapper.sln -c Release
```

The repository intentionally ignores generated exports, game files, build
outputs, Visual Studio state, and local validation folders.
