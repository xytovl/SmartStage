#!/bin/sh

if [ "$#" != 2 ] ; then
	echo "usage: $0 project_name file"
	exit 1
fi

set -e

RESOURCES="$1/SmartStage.tga"

TARGET="GameData/$1"
OUTPUT="$1-`git describe --abbrev=0 --tags|sed s/^v//`.zip"

rm -rf GameData $OUTPUT
mkdir -p $TARGET/Plugins

cp $2 $TARGET/Plugins

for f in $RESOURCES ;do
	cp $f $TARGET
done

zip -r $OUTPUT GameData
rm -r GameData
