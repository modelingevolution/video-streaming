# Makefile for building LibJpegWrap.so
# execute: make USE_TURBO=1

# Compiler settings
CXX = g++
CXXFLAGS = -fPIC -Wall -O3

# Source and target files
SRC = LibJpegWrap.cpp
OBJ = $(SRC:.cpp=.o)
TARGET = LibJpegWrap.so
INSTALL_LIB_DIR = /usr/local/lib

# Default target
all: $(TARGET)

# Check for USE_TURBO switch
ifdef USE_TURBO
    CXXFLAGS += -I/opt/libjpeg-turbo/include
    LIBDIR = /opt/libjpeg-turbo/lib64/
    LIBS = -lturbojpeg -ljpeg
else
    LIBDIR = /usr/lib/x86_64-linux-gnu/
    LIBS = -ljpeg
endif

$(TARGET): $(OBJ)
	$(CXX) -shared -L$(LIBDIR) -o $@ $^ $(LIBS)

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