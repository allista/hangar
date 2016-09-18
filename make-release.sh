#!/bin/bash

cd $(dirname "$0")

./make_release -s 'Source' -e '*/ForModders/*' '*/config.xml' '*/Hangar.user' '*.orig'
