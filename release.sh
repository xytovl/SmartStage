#!/bin/sh

if [ "$#" != 2 ] ; then
	echo "usage: $0 project_name file"
	exit 1
fi

set -e

TARGET="GameData/$1/Plugins"
rm -rf GameData
mkdir -p $TARGET
cp $2 $TARGET
zip -r "$1-`git describe --abbrev=0 --tags|sed s/^v//`.zip" GameData
rm -r GameData
