# Makefile for building LibJpegWrap.so

# Compiler settings
CXX = g++
CXXFLAGS = -fPIC -Wall -I/opt/libjpeg-turbo/include -O3
LDFLAGS = -shared
LIBDIR = /opt/libjpeg-turbo/lib64/
LIBS = -lturbojpeg -ljpeg

# Source and target files
SRC = LibJpegWrap.cpp
OBJ = $(SRC:.cpp=.o)
TARGET = LibJpegWrap.so
INSTALL_LIB_DIR = /usr/local/lib

# Default target
all: $(TARGET)

$(TARGET): $(OBJ)
	$(CXX) $(LDFLAGS) -L$(LIBDIR) -o $@ $^ $(LIBS)

%.o: %.cpp
	$(CXX) $(CXXFLAGS) -c $< -o $@

install: $(TARGET)
	cp $(TARGET) $(INSTALL_LIB_DIR)
	# cp $(HEADER) $(INSTALL_INCLUDE_DIR)
	ldconfig # Update the shared library cache

# Clean target
clean:
	rm -f $(OBJ) $(TARGET)

.PHONY: all clean
