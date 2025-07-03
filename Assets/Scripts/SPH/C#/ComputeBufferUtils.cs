using UnityEngine;
using System;

/// <summary>
/// Utility class for safe ComputeBuffer operations
/// </summary>
public static class ComputeBufferUtils
{
    /// <summary>
    /// Safely dispose a ComputeBuffer
    /// </summary>
    public static void SafeDispose(ref ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            try
            {
                buffer.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error disposing ComputeBuffer: {e.Message}");
            }
            finally
            {
                buffer = null;
            }
        }
    }

    /// <summary>
    /// Check if a ComputeBuffer is valid and ready to use
    /// </summary>
    public static bool IsValid(ComputeBuffer buffer)
    {
        return buffer != null && buffer.IsValid();
    }
}