#!/bin/bash

cd $(dirname "$0")

#get the latest version by git tag
version=$(grep AssemblyVersion Source/AssemblyInfo.cs | sed "s:.*\"\(.*\)\".*:\1:")
archive="Hangar-v$version.zip"

#create zip archive
zip -r -9 Releases/$archive GameData -x "*~" "*/config.xml" "*/ForModders/*" || exit 1
zip -T Releases/$archive || exit 2
echo
echo "$archive created in Releases/"

