   cmake_minimum_required(VERSION 3.8)
   
   # Enable Hot Reload for MSVC compilers if supported.
   if (POLICY CMP0141)
     cmake_policy(SET CMP0141 NEW)
     #set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
     set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
   endif()
 
   project("HailoProcessor")
   # Find OpenCV package
   find_package(OpenCV REQUIRED)
   find_package(HailoRT REQUIRED) 
   find_package(Threads)
  
   # Include OpenCV headers
   include_directories(${OpenCV_INCLUDE_DIRS} )
   include_directories(${HailoRT_INCLUDE_DIRS})
   # Include sub-projects.
   add_subdirectory("src")
   enable_testing() 
   add_subdirectory("tests")
