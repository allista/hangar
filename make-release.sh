#!/bin/bash

cd $(dirname "$0")

modname=Hangar
assembly_info=Source/AssemblyInfo.cs
exclude=make-release.exclude

#write exclude patterns to a temporary file
cat <<EOF >> $exclude 
*~
*.bak
*/config.xml
*/ForModders/*
EOF

#parse current version
version=$(grep AssemblyVersion $assembly_info | sed "s:.*\"\(.*\)\".*:\1:")
archive="$modname-v$version.zip"

end()
{
	rm -f $exclude
	exit $1
}

#create zip archive
mkdir -p Releases
[[ -f Releases/$archive ]] && mv Releases/$archive Releases/$archive.back
zip -r -9 Releases/$archive GameData --exclude @$exclude || end 1
zip -T Releases/$archive || end 2

#exit gracefully
echo
echo "$archive created in Releases/"
end 0

