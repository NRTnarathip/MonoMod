cd "$(dirname "$0")"

g++ -g -Wall -O2 -shared -std=c++11 -o macos_helper_arm64.dylib macos_helper_arm64.cpp
