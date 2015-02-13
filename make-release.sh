#!/bin/bash

cd $(dirname "$0")

modname=Hangar
assembly_info=Source/AssemblyInfo.cs

version=$(grep AssemblyVersion $assembly_info | sed "s:.*\"\(.*\)\".*:\1:")
archive="$modname-v$version.zip"

#create zip archive
zip -r -9 Releases/$archive GameData -x "*~" "*/config.xml" "*/ForModders/*" || exit 1
zip -T Releases/$archive || exit 2
echo
echo "$archive created in Releases/"

