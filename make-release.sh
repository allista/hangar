#!/bin/bash

cd $(dirname "$0")

../../PyKSPutils/make_mod_release  -s 'Source' \
-e '*/ForModders/*' '*/config.xml' '*.user' '*.orig' '*.mdb' \
-i '../AT_Utils/GameData' '../AT_Utils/ConfigurableContainers/GameData'
