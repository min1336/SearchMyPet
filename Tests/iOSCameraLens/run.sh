#!/bin/sh
set -eu

if [ "$#" -ne 1 ]; then
    echo "usage: $0 /path/to/SearchMyPetARCameraLens.mm" >&2
    exit 2
fi

plugin_path=$1
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
build_dir=${TMPDIR:-/tmp}/searchmypet-ios-camera-lens-tests
binary_path=$build_dir/SearchMyPetARCameraLensTests

mkdir -p "$build_dir"
xcrun clang++ \
    -std=c++17 \
    -fobjc-arc \
    -framework Foundation \
    -I "$script_dir/Fakes" \
    "$plugin_path" \
    "$script_dir/SearchMyPetARCameraLensTests.mm" \
    -o "$binary_path"

"$binary_path"
