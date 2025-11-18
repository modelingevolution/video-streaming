//
// Created by pi on 30/11/24.
//

#include "HailoException.h"

HailoException::HailoException(const hailo_status st) : std::exception(), _status(st)
{
    this->_msg = hailo_get_status_message(st);
}

HailoException::HailoException(const string& str) : _status(HAILO_SUCCESS)
{
    this->_msg = str.c_str();
}

HailoException::HailoException(const HailoException& c) : _status(c._status), _msg(c._msg)
{


}

hailo_status HailoException::GetStatus()
{
    return this->_status;
}

const char* HailoException::what() const noexcept
{
    return this->_msg;
}