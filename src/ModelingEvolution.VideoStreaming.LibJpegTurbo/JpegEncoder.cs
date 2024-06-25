﻿using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming.LibJpegTurbo;

public class JpegEncoder : IDisposable
{
    private int _mode = 0;
    private int _quality = 90;
    private readonly IntPtr _encoderPtr;
    private bool _disposed;
    [DllImport("LibJpegWrap.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Create(int width, int height, int quality, ulong bufSize);

    [DllImport("LibJpegWrap.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Encode")]
    private static extern ulong OnEncode(IntPtr encoder, nint data, nint dstBuffer, ulong dstBufferSize);

    [DllImport("LibJpegWrap.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Close(IntPtr encoder);


    [DllImport("LibJpegWrap.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetMode(IntPtr encoder, int mode);

    [DllImport("LibJpegWrap.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetQuality(IntPtr encoder, int quality);


    public JpegEncoder(int width, int height, int quality, ulong minimumBufferSize)
    {
        _quality = quality;
        _encoderPtr = Create(width, height, quality, minimumBufferSize);
    }

       
    public ulong Encode(nint data, nint dst, ulong dstBufferSize)
    {
        return OnEncode(_encoderPtr, data,dst, dstBufferSize);
    }
    
    public unsafe ulong Encode(nint data, byte[] dst)
    {
        if (_encoderPtr != IntPtr.Zero)
            fixed (byte* dstPtr = dst)
            {
                return OnEncode(_encoderPtr, data, (nint)dstPtr, (ulong)dst.LongLength);
            }
        return 0;
    }
    public unsafe ulong Encode(byte[] data, byte[] dst)
    {
        if (_encoderPtr != IntPtr.Zero)
            // Pin the byte[] array
            fixed (byte* p = data)
            fixed (byte* dstPtr = dst)
            {
                return OnEncode(_encoderPtr, (nint)p, (nint)dstPtr, (ulong)dst.LongLength);
            }

        return 0;
    }
    public DiscreteCosineTransform Mode
    {
        get => (DiscreteCosineTransform)_mode;
        set
        {
            if(_mode == (int)value) return;
            _mode = (int)value;
            SetMode(_encoderPtr, _mode);
        }
    }
    public int Quality
    {
        get => _quality;
        set
        {
            if(_quality == value)  return;
            _quality = value;
            SetQuality(_encoderPtr, value);
        }
    }

    ~JpegEncoder()
    {
        Dispose();
    }
    public void Dispose()
    {
        if (_encoderPtr == IntPtr.Zero || _disposed) return;
        Close(_encoderPtr);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
public enum DiscreteCosineTransform
{
    // Slow
    Integer = 0,
    // Fast
    Float = 1
}