#!/bin/bash

find GameData/ -name '*.cfg' -exec aspell -c '{}' \;
