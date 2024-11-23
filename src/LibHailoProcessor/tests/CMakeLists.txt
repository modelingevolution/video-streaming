# FetchContent to download GoogleTest automatically
include(FetchContent)
FetchContent_Declare(
    googletest
    URL https://github.com/google/googletest/archive/refs/heads/main.zip
)
FetchContent_MakeAvailable(googletest)
enable_testing()
# Add test executable
add_executable(MyTests test_main.cpp) 

#target_link_libraries(MyTests PRIVATE HailoProcessor )
target_link_libraries(MyTests PRIVATE HailoProcessor gtest_main gtest )

# Register the test
#add_test(NAME MyTests COMMAND MyTests)
#gtest_discover_tests(test_main)
include(GoogleTest)
gtest_discover_tests(MyTests)