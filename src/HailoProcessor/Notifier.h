#ifndef NOTIFIER_H
#define NOTIFIER_H

#include <mutex>
#include <condition_variable>

class Notifier {
public:
    Notifier();
    ~Notifier();
    void EnqueueWork();
    bool Wait();
    void Dispose();

private:

    std::mutex m_mutex;
    std::condition_variable m_cv;
    bool _hasWork = false;
    bool _disposed = false;
};



#endif // NOTIFIER_H
