#!/bin/bash

cd $(dirname "$0")
pwd

API=KSP_API
echo "Cleaning .cs files in $API"

files="$(find "$API" -name "*.cs")"
[[ -n "$files" ]] || { echo "No .cs files was found"; exit; }

for f in $files; do
	before=$(ls --block-size=1 -s "$f")
	#remove 'while (true) {}' blocks
	perl -i -0pe 's/\s*while \(true\)\r*\n\s*{\r*\n\s*switch \(\d\)\r*\n\s*{\r*\n\s*case 0:\r*\n\s*continue;\r*\n\s*}\r*\n\s*break;\r*\n\s*}//mg' "$f"
	#remove 'if (!true) {}' blocks
	perl -i -0pe 's/\s*if \(\!true\)\r*\n\s*{(\r*\n\s*[^}].*\r*\n)+\s*}//mg' "$f"
	#remove empty lines
	perl -i -0pe 's/^\s*\r*\n//mg' "$f"
	#check the result
	after=$(ls --block-size=1 -s "$f")
	[[ "$before" == "$after" ]] || { echo "Cleaned $f:"; echo $before; echo $after; }
done