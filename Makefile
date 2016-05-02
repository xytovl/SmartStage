# Makefile for building SmartStage (adapted from MechJeb)

KSPDIR  := ${HOME}/.local/share/Steam/SteamApps/common/Kerbal\ Space\ Program
MANAGED := ${KSPDIR}/KSP_Data/Managed/

SOURCEFILES := $(wildcard SmartStage/*.cs)\
	$(wildcard SmartStage/GUI/*.cs)\
	$(wildcard SmartStage/Aero/*.cs)

RESGEN2 := resgen2
GMCS    := gmcs
GIT     := git
TAR     := tar
ZIP     := zip

VERSION_MAJOR := 2
VERSION_MINOR := 9
VERSION_PATCH := 4

VERSION := ${VERSION_MAJOR}.${VERSION_MINOR}.${VERSION_PATCH}

ifeq ($(debug),1)
	DEBUG = -debug -define:DEBUG
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
		-r:Assembly-CSharp,Assembly-CSharp-firstpass,UnityEngine,KSPUtil,UnityEngine.UI \
		${DEBUG} \
		-out:$@ \
		${SOURCEFILES}


package: build/SmartStage.dll SmartStage.version
	mkdir -p package/SmartStage/Plugins
#	cp -r Parts package/SmartStage
	cp img/* package/SmartStage/
	cp SmartStage.version package/SmartStage
	cp $< package/SmartStage/Plugins/

%.zip:
	cd package && ${ZIP} -9 -r ../$@ SmartStage

zip: package SmartStage-${VERSION}.zip

SmartStage.version: SmartStage.version.in Makefile
	sed -e 's/@MAJOR@/'${VERSION_MAJOR}/g -e 's/@MINOR@/'${VERSION_MINOR}/g -e 's/@PATCH@/'${VERSION_PATCH}/g < SmartStage.version.in > SmartStage.version

release: SmartStage.version zip
	git commit -m "release v${VERSION}" Makefile SmartStage.version
	git tag v${VERSION}

clean:
	@echo "Cleaning up build and package directories..."
	rm -rf build/ package/

install: package
	cp -r package/SmartStage ${KSPDIR}/GameData/

uninstall: info
	rm -rf ${KSPDIR}/GameData/SmartStage


.PHONY : all info build package tar.gz zip clean install uninstall
