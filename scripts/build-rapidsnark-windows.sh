#!/usr/bin/env bash
# Build rapidsnark native DLL for Windows x64 using MSYS2/MinGW64.
#
# Prerequisites (one-time):
#   winget install MSYS2.MSYS2
#   C:/msys64/usr/bin/bash.exe -lc "pacman -S --noconfirm mingw-w64-x86_64-gcc mingw-w64-x86_64-cmake make m4 diffutils tar xz"
#
# Usage:
#   C:/msys64/usr/bin/env.exe MSYSTEM=MINGW64 C:/msys64/usr/bin/bash.exe -l scripts/build-rapidsnark-windows.sh
#
# Or from MSYS2 MinGW64 shell:
#   bash scripts/build-rapidsnark-windows.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
RAPIDSNARK_DIR="${RAPIDSNARK_DIR:-$REPO_ROOT/../rapidsnark}"
OUTPUT_DIR="$REPO_ROOT/src/Nethereum.ZkProofs.RapidSnark/runtimes/win-x64/native"

echo "=== Nethereum rapidsnark Windows build ==="
echo "rapidsnark source: $RAPIDSNARK_DIR"
echo "output dir:        $OUTPUT_DIR"

if [ ! -d "$RAPIDSNARK_DIR" ]; then
    echo "Cloning iden3/rapidsnark..."
    git clone --recursive https://github.com/iden3/rapidsnark.git "$RAPIDSNARK_DIR"
fi

cd "$RAPIDSNARK_DIR"

# Ensure submodules
git submodule update --init --recursive

# ---- Step 1: Build GMP ----
echo ""
echo "=== Building GMP 6.3.0 ==="
GMP_DIR="$RAPIDSNARK_DIR/depends/gmp"

if [ -f "$GMP_DIR/package/lib/libgmp.a" ]; then
    echo "GMP already built, skipping."
else
    cd "$GMP_DIR"

    # Download GMP if needed
    GMP_ARCHIVE="gmp-6.3.0.tar.xz"
    if [ ! -f "$GMP_ARCHIVE" ]; then
        echo "Downloading GMP..."
        curl -L -O "https://ftpmirror.gnu.org/gmp/$GMP_ARCHIVE" || \
        curl -L -O "https://gmplib.org/download/gmp/$GMP_ARCHIVE" || \
        curl -L -O "https://ftp.gnu.org/gnu/gmp/$GMP_ARCHIVE"
    fi

    if [ ! -d "gmp" ]; then
        tar -xf "$GMP_ARCHIVE"
        mv gmp-6.3.0 gmp
    fi

    cd gmp
    rm -rf build_win package

    mkdir build_win && cd build_win
    # -std=gnu17 required for GCC 14+/15+ compat with GMP configure tests
    CC=gcc CFLAGS="-O2 -std=gnu17" ../configure \
        --prefix="$GMP_DIR/package" \
        --with-pic --disable-fft --disable-assembly
    make -j"$(nproc)"
    make install
    echo "GMP installed to $GMP_DIR/package"
fi

# ---- Step 2: Apply Windows portability patches ----
echo ""
echo "=== Applying Windows patches ==="
cd "$RAPIDSNARK_DIR"

# Fix u_int types (BSD → standard C++) in all source files
fix_uint_types() {
    local file="$1"
    if grep -q 'u_int' "$file" 2>/dev/null; then
        sed -i 's/u_int64_t/uint64_t/g; s/u_int32_t/uint32_t/g' "$file"
        echo "  Fixed u_int types in $file"
    fi
}

for f in src/binfile_utils.hpp src/binfile_utils.cpp src/zkey_utils.hpp \
         src/wtns_utils.hpp src/prover.cpp src/groth16.hpp src/groth16.cpp \
         depends/ffiasm/c/fft.hpp depends/ffiasm/c/fft.cpp \
         depends/ffiasm/c/multiexp.cpp depends/ffiasm/c/pointparallelprocessor.hpp \
         depends/ffiasm/c/binfile_utils.hpp depends/ffiasm/c/binfile_utils.cpp \
         depends/ffiasm/c/wtns_utils.hpp depends/ffiasm/c/zkey_utils.hpp; do
    fix_uint_types "$f"
done

# Add #include <cstdint> where needed (idempotent)
add_cstdint() {
    local file="$1"
    if ! grep -q '#include <cstdint>' "$file" 2>/dev/null; then
        sed -i '1i #include <cstdint>' "$file"
        echo "  Added <cstdint> to $file"
    fi
}

for f in src/binfile_utils.hpp src/zkey_utils.hpp src/wtns_utils.hpp \
         src/groth16.hpp depends/ffiasm/c/fft.hpp depends/ffiasm/c/multiexp.cpp \
         depends/ffiasm/c/pointparallelprocessor.hpp; do
    add_cstdint "$f"
done

# Remove POSIX-only includes from binfile_utils.cpp (idempotent)
if grep -q 'sys/mman.h' src/binfile_utils.cpp 2>/dev/null; then
    sed -i '/#include <sys\/mman.h>/d; /#include <sys\/stat.h>/d; /#include <fcntl.h>/d; /#include <unistd.h>/d' src/binfile_utils.cpp
    echo "  Removed POSIX includes from binfile_utils.cpp"
fi

# Patch fileloader for Windows (CreateFileMapping/MapViewOfFile)
if ! grep -q '_WIN32' src/fileloader.hpp 2>/dev/null; then
    echo "  Patching fileloader.hpp/cpp for Windows..."
    cat > src/fileloader.hpp << 'HPPEOF'
#ifndef FILELOADER_HPP
#define FILELOADER_HPP

#include <cstddef>
#include <string>

#ifdef _WIN32
#include <windows.h>
#endif

namespace BinFileUtils {

class FileLoader
{
public:
    FileLoader();
    FileLoader(const std::string& fileName);
    ~FileLoader();

    void load(const std::string& fileName);

    void*  dataBuffer() { return addr; }
    size_t dataSize() const { return size; }

    std::string dataAsString() { return std::string((char*)addr, size); }

private:
    void*   addr;
    size_t  size;
#ifdef _WIN32
    HANDLE  hFile;
    HANDLE  hMapping;
#else
    int     fd;
#endif
};

}

#endif // FILELOADER_HPP
HPPEOF

    cat > src/fileloader.cpp << 'CPPEOF'
#ifdef _WIN32
#include <windows.h>
#include <stdexcept>
#include <system_error>
#else
#include <sys/mman.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <unistd.h>
#include <system_error>
#include <stdexcept>
#endif

#include "fileloader.hpp"

namespace BinFileUtils {

#ifdef _WIN32

FileLoader::FileLoader()
    : addr(nullptr), size(0), hFile(INVALID_HANDLE_VALUE), hMapping(nullptr)
{
}

FileLoader::FileLoader(const std::string& fileName)
    : addr(nullptr), size(0), hFile(INVALID_HANDLE_VALUE), hMapping(nullptr)
{
    load(fileName);
}

void FileLoader::load(const std::string& fileName)
{
    if (hFile != INVALID_HANDLE_VALUE) {
        throw std::invalid_argument("file already loaded");
    }

    hFile = CreateFileA(fileName.c_str(), GENERIC_READ, FILE_SHARE_READ,
                        nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (hFile == INVALID_HANDLE_VALUE) {
        throw std::system_error(GetLastError(), std::system_category(), "CreateFile");
    }

    LARGE_INTEGER fileSize;
    if (!GetFileSizeEx(hFile, &fileSize)) {
        CloseHandle(hFile);
        hFile = INVALID_HANDLE_VALUE;
        throw std::system_error(GetLastError(), std::system_category(), "GetFileSizeEx");
    }

    size = static_cast<size_t>(fileSize.QuadPart);

    hMapping = CreateFileMappingA(hFile, nullptr, PAGE_READONLY, 0, 0, nullptr);
    if (hMapping == nullptr) {
        CloseHandle(hFile);
        hFile = INVALID_HANDLE_VALUE;
        throw std::system_error(GetLastError(), std::system_category(), "CreateFileMapping");
    }

    addr = MapViewOfFile(hMapping, FILE_MAP_READ, 0, 0, 0);
    if (addr == nullptr) {
        CloseHandle(hMapping);
        CloseHandle(hFile);
        hMapping = nullptr;
        hFile = INVALID_HANDLE_VALUE;
        throw std::system_error(GetLastError(), std::system_category(), "MapViewOfFile");
    }
}

FileLoader::~FileLoader()
{
    if (addr != nullptr) {
        UnmapViewOfFile(addr);
    }
    if (hMapping != nullptr) {
        CloseHandle(hMapping);
    }
    if (hFile != INVALID_HANDLE_VALUE) {
        CloseHandle(hFile);
    }
}

#else

FileLoader::FileLoader()
    : fd(-1)
{
}

FileLoader::FileLoader(const std::string& fileName)
    : fd(-1)
{
    load(fileName);
}

void FileLoader::load(const std::string& fileName)
{
    if (fd != -1) {
        throw std::invalid_argument("file already loaded");
    }

    struct stat sb;

    fd = open(fileName.c_str(), O_RDONLY);
    if (fd == -1)
        throw std::system_error(errno, std::generic_category(), "open");

    if (fstat(fd, &sb) == -1) {
        close(fd);
        throw std::system_error(errno, std::generic_category(), "fstat");
    }

    size = sb.st_size;

    addr = mmap(nullptr, size, PROT_READ, MAP_PRIVATE, fd, 0);

    if (addr == MAP_FAILED) {
        close(fd);
        throw std::system_error(errno, std::generic_category(), "mmap failed");
    }

    madvise(addr, size, MADV_SEQUENTIAL);
}

FileLoader::~FileLoader()
{
    if (fd != -1) {
        munmap(addr, size);
        close(fd);
    }
}

#endif

} // Namespace
CPPEOF
fi

# ---- Step 3: Build rapidsnark ----
echo ""
echo "=== Building rapidsnark DLL ==="
cd "$RAPIDSNARK_DIR"

rm -rf build_prover_win
mkdir build_prover_win && cd build_prover_win

cmake .. -G "MSYS Makefiles" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX=../package_win \
    -DUSE_ASM=NO \
    -DUSE_OPENMP=OFF \
    -DBUILD_TESTS=OFF \
    -DCMAKE_CXX_FLAGS="-Duint=unsigned -std=c++17" \
    -DCMAKE_C_FLAGS="-std=gnu17 -Duint=unsigned" \
    -DCMAKE_SHARED_LINKER_FLAGS="-static-libgcc -static-libstdc++ -static"

make -j"$(nproc)" rapidsnark

# ---- Step 4: Copy to Nethereum runtimes ----
echo ""
echo "=== Copying DLL ==="
mkdir -p "$OUTPUT_DIR"
cp src/librapidsnark.dll "$OUTPUT_DIR/rapidsnark.dll"

echo ""
echo "=== Build complete ==="
ls -lh "$OUTPUT_DIR/rapidsnark.dll"

# Verify no MinGW runtime dependencies
echo ""
echo "DLL dependencies:"
objdump -p "$OUTPUT_DIR/rapidsnark.dll" | grep "DLL Name" || true
