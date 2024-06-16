# Makefile for building LibJpegWrap.so

# Compiler settings
CXX = g++
CXXFLAGS = -fPIC -Wall -I/opt/libjpeg-turbo/include
LDFLAGS = -shared
LIBS = 

# Source and target files
SRC = LibJpegWrap.cpp
OBJ = $(SRC:.cpp=.o)
TARGET = LibJpegWrap.so

# Default target
all: $(TARGET)

$(TARGET): $(OBJ)
	$(CXX) $(LDFLAGS) -o $@ $^ $(LIBS)

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