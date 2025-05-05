namespace EigenAstroSim.Domain

open System
open System.Collections.Generic

module BufferManagement =
    type BufferPool(width: int, height: int) =
        let bufferStack = new Stack<float[,]>()
        let mutable created = 0
        let mutable reused = 0
        
        /// Create a new buffer of the specified size
        let createBuffer() =
            created <- created + 1
            Array2D.zeroCreate width height
            
        /// Number of buffers currently in the pool
        member _.PoolSize = bufferStack.Count
        
        /// Total number of buffers created
        member _.Created = created
        
        /// Number of buffers reused
        member _.Reused = reused
        
        /// Get a buffer from the pool or create a new one
        member _.GetBuffer() =
            if bufferStack.Count > 0 then
                reused <- reused + 1
                let buffer = bufferStack.Pop()
                // Clear the buffer
                for i = 0 to width - 1 do
                    for j = 0 to height - 1 do
                        buffer.[i, j] <- 0.0
                buffer
            else
                createBuffer()
                
        /// Return a buffer to the pool
        member _.ReturnBuffer(buffer: float[,]) =
            bufferStack.Push(buffer)
            
        /// Clear all buffers from the pool
        member _.Clear() =
            bufferStack.Clear()

    /// Buffer pool manager class for reusing array allocations
    type BufferPoolManager() =
        let bufferPools = Dictionary<int*int, BufferPool>()
        
        /// Get a buffer pool for the specified dimensions
        member _.GetBufferPool (width: int) (height: int) =
            let key = (width, height)
            if not (bufferPools.ContainsKey(key)) then
                bufferPools.Add(key, new BufferPool(width, height))
            bufferPools.[key]
            
        /// Clear all buffer pools
        member _.ClearAll() =
            for pool in bufferPools.Values do
                pool.Clear()
            bufferPools.Clear()
            
        interface IDisposable with
            member this.Dispose() =
                this.ClearAll()
    /// Represents a buffer that will be automatically returned to the pool when disposed
    type PooledBuffer(manager: BufferPoolManager, width: int, height: int) =
        let pool = manager.GetBufferPool width height
        let buffer = pool.GetBuffer()
        let mutable disposed = false
        
        // Rest remains similar, just using the provided manager instance
        
        member _.Buffer = buffer
        member _.Width = width
        member _.Height = height
        
        member _.Dispose() =
            if not disposed then
                pool.ReturnBuffer(buffer)
                disposed <- true
                
        interface IDisposable with
            member this.Dispose() = this.Dispose()

    /// Create a new pooled buffer with the specified dimensions
    let createPooledBuffer (manager: BufferPoolManager) (width: int) (height: int) =
        new PooledBuffer(manager, width, height)
        
    /// Use a pooled buffer for a computation and return it to the pool
    let usePooledBuffer (manager: BufferPoolManager) (width: int) (height: int) (action: float[,] -> 'a) =
        use pooledBuffer = createPooledBuffer manager width height
        action pooledBuffer.Buffer
