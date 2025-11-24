#include "Notifier.h"
#include <iostream>

Notifier::Notifier() {}

void Notifier::EnqueueWork() {
    std::lock_guard<std::mutex> lock(m_mutex);
    _hasWork = true;
    //std::cout << "work enqueued" << std::endl;
    m_cv.notify_one();
}

bool Notifier::Wait() {
    std::unique_lock<std::mutex> lock(m_mutex);
    m_cv.wait(lock, [this]{ return _hasWork || _disposed; });
    bool work = _hasWork;
    _hasWork = false;
    //std::cout << "work denqued" << std::endl;
    return work && !_disposed;
}


Notifier::~Notifier() {
    if(!_disposed)
      Dispose();
}

void Notifier::Dispose() {
    std::lock_guard<std::mutex> lock(m_mutex);
    _disposed = true;
    m_cv.notify_all();
}