#!/bin/bash

declare -A COMPILER=( [x86_64]=/usr/bin/gcc
                      [aarch64]=/usr/bin/aarch64-linux-gnu-gcc
                      [armv7l]=/usr/bin/arm-linux-gnueabi-gcc )

for ARCH in aarch64
do
  echo "-I- Building ${ARCH}"
  mkdir -p build/${ARCH}

  # Configure for the first build or if CMakeLists.txt has changed
  if [ ! -f build/${ARCH}/Makefile ] || [ build/${ARCH}/CMakeLists.txt -nt build/${ARCH}/Makefile ]; then
    cmake -H. -Bbuild/${ARCH} -DCMAKE_BUILD_TYPE=Release  # Add build type (optional)
  fi

  # Build using make
  make -C build/${ARCH}
done

if [[ -f "hailort.log" ]]; then
  rm hailort.log
fi