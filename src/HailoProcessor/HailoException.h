//
// Created by pi on 30/11/24.
//

#ifndef HAILOEXCEPTION_H
#define HAILOEXCEPTION_H

#include <hailo/hailort.h>
#include <hailo/hailort_common.hpp>
#include <string>


using namespace std;

class HailoException : public std::exception
{
public:
    HailoException(const hailo_status st);
    HailoException(const string& str);
    HailoException(const HailoException& c);
    hailo_status GetStatus();
    virtual const char* what() const noexcept override;
private:
    const hailo_status _status;
    const char* _msg;
};



#endif //HAILOEXCEPTION_H
