#!/bin/sh
# cloud build with mcs (the csproj + dotnet is the normal path on Windows)
M=${MANAGED:-/mnt/user-data/uploads/common--Apocalypter/Apocalypter_Data/Managed}; B=${BEPCORE:-/mnt/user-data/uploads/common--Apocalypter/BepInEx/core}
mcs -nostdlib -noconfig -target:library -langversion:latest -optimize+ -out:${1:-PartAdjustment.dll} \
  -r:$M/mscorlib.dll -r:$M/System.dll -r:$M/System.Core.dll -r:$M/netstandard.dll \
  -r:$B/BepInEx.dll -r:$B/0Harmony.dll -r:$M/UnityEngine.dll -r:$M/UnityEngine.CoreModule.dll -r:$M/UnityEngine.InputLegacyModule.dll \
  -r:$M/UnityEngine.PhysicsModule.dll -r:$M/UnityEngine.AudioModule.dll -r:$M/UnityEngine.UI.dll -r:$M/UnityEngine.UIModule.dll -r:$M/UnityEngine.TextRenderingModule.dll \
  -r:$M/PlayMaker.dll -r:$M/Assembly-CSharp.dll -r:$M/Assembly-CSharp-firstpass.dll -r:$M/NWH.WheelController.dll -r:$M/NWH.Common.dll \
  *.cs
