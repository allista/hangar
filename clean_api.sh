#!/bin/bash

cd $(dirname "$0")

API=KSP_API

#remove while(true)
find $API -name "*.cs" -print0 | xargs -0 perl -i -0pe 's/while \(true\)\n\s*{\n\s*switch \(\d\)\n\s*{\n\s*case 0:\n\s*continue;\n\s*}\n\s*break;\n\s*}//mg'

#remove if(!true)
find $API -name "*.cs" -print0 | xargs -0 perl -i -0pe 's/if \(\!true\)\n\s*{\n\s*RuntimeMethodHandle .*\n\s*}//mg'

#remove empty lines
find $API -name "*.cs" -print0 | xargs -0 perl -i -0pe 's/^\s*\n//mg'