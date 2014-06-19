# Makefile for building SmartStage (adapted from MechJeb)

KSPDIR  := ${HOME}/.local/share/Steam/SteamApps/common/Kerbal\ Space\ Program
MANAGED := ${KSPDIR}/KSP_Data/Managed/

SOURCEFILES := $(wildcard SmartStage/*.cs)

RESGEN2 := resgen2
GMCS    := gmcs
GIT     := git
TAR     := tar
ZIP     := zip

VERSION := $(shell ${GIT} describe --abbrev=0 --tags|sed s/^v//)

ifeq ($(debug),1)
	DEBUG = -debug
endif

all: build/SmartStage.dll

info:
	@echo "== SmartStage Build Information =="
	@echo "  resgen2: ${RESGEN2}"
	@echo "  gmcs:    ${GMCS}"
	@echo "  git:     ${GIT}"
	@echo "  tar:     ${TAR}"
	@echo "  zip:     ${ZIP}"
	@echo "  KSP Data: ${KSPDIR}"
	@echo "================================"

build/%.dll: ${SOURCEFILES}
	mkdir -p build
	${GMCS} -t:library -lib:${MANAGED} \
		-r:Assembly-CSharp,Assembly-CSharp-firstpass,UnityEngine \
		${DEBUG} \
		-out:$@ \
		${SOURCEFILES}


package: build/SmartStage.dll
	mkdir -p package/SmartStage/Plugins
#	cp -r Parts package/SmartStage
	cp img/* package/SmartStage/
	cp $< package/SmartStage/Plugins/

%.zip:
	cd package && ${ZIP} -9 -r ../$@ SmartStage

zip: package SmartStage-${VERSION}.zip


clean:
	@echo "Cleaning up build and package directories..."
	rm -rf build/ package/

install: package
	cp -r package/SmartStage ${KSPDIR}/GameData/

uninstall: info
	rm -rf ${KSPDIR}/GameData/SmartStage


.PHONY : all info build package tar.gz zip clean install uninstall
