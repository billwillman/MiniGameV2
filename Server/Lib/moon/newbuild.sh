#!/bin/bash

git submodule init
git submodule update
git pull

# Detect the operating system
os=$(uname)

# Execute Linux commands
echo "Running on Linux"
cpu_num=$(cat /proc/cpuinfo | grep "processor" | wc -l)
make config=release -j$cpu_num