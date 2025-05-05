namespace EigenAstroSim.Tests

module BufferManagerTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain.BufferManagement

    [<Fact>]
    let ``New buffer pool starts with zero created buffers`` () =
        use manager = new BufferPoolManager()
        let pool = manager.GetBufferPool 100 100
        
        pool.Created |> should equal 0
        pool.Reused |> should equal 0
        
    [<Fact>]
    let ``Getting a buffer increments created count`` () =
        use manager = new BufferPoolManager()
        let pool = manager.GetBufferPool 100 100
        
        let buffer = pool.GetBuffer()
        
        pool.Created |> should equal 1
        pool.Reused |> should equal 0
        
    [<Fact>]
    let ``Buffer pool should reuse buffers`` () =
        use manager = new BufferPoolManager()
        let pool = manager.GetBufferPool 100 100
        
        // First buffer should be new
        let buffer1 = pool.GetBuffer()
        pool.ReturnBuffer(buffer1)
        
        // Second buffer should be reused
        let buffer2 = pool.GetBuffer()
        
        pool.Created |> should equal 1
        pool.Reused |> should equal 1
        
        // The same buffer instance should be returned
        Object.ReferenceEquals(buffer1, buffer2) |> should equal true
        
    [<Fact>]
    let ``Returned buffer should be cleared for reuse`` () =
        use manager = new BufferPoolManager()
        let pool = manager.GetBufferPool 10 10
        
        // Get first buffer and fill it with data
        let buffer1 = pool.GetBuffer()
        for i = 0 to 9 do
            for j = 0 to 9 do
                buffer1.[i, j] <- 42.0
                
        // Return and get it again
        pool.ReturnBuffer(buffer1)
        let buffer2 = pool.GetBuffer()
        
        // Should be same instance but cleared to zeros
        Object.ReferenceEquals(buffer1, buffer2) |> should equal true
        
        // Check all values are zero
        let allZeros = 
            seq {
                for i = 0 to 9 do
                    for j = 0 to 9 do
                        yield buffer2.[i, j] = 0.0
            } |> Seq.forall id
            
        allZeros |> should equal true
        
    [<Fact>]
    let ``Multiple pool dimensions should be isolated`` () =
        use manager = new BufferPoolManager()
        
        // Create two pools with different dimensions
        let pool1 = manager.GetBufferPool 100 100
        let pool2 = manager.GetBufferPool 200 200
        
        // Get a buffer from each
        let buffer1 = pool1.GetBuffer()
        let buffer2 = pool2.GetBuffer()
        
        // Each pool should have one created buffer
        pool1.Created |> should equal 1
        pool2.Created |> should equal 1
        
        // Buffer dimensions should match requested dimensions
        (Array2D.length1 buffer1, Array2D.length2 buffer1) |> should equal (100, 100)
        (Array2D.length1 buffer2, Array2D.length2 buffer2) |> should equal (200, 200)

    [<Fact>]
    let ``Different manager instances should be isolated`` () =
        use manager1 = new BufferPoolManager()
        use manager2 = new BufferPoolManager()
        
        // Get a pool with same dimensions from each manager
        let pool1 = manager1.GetBufferPool 100 100
        let pool2 = manager2.GetBufferPool 100 100
        
        // Get and return a buffer in the first pool
        let buffer1 = pool1.GetBuffer()
        pool1.ReturnBuffer(buffer1)
        
        // Check stats - first pool should have activity, second should not
        pool1.Created |> should equal 1
        pool1.Reused |> should equal 0
        pool2.Created |> should equal 0
        pool2.Reused |> should equal 0
        
    [<Fact>]
    let ``ClearAll should only affect that manager instance`` () =
        use manager1 = new BufferPoolManager()
        use manager2 = new BufferPoolManager()
        
        // Create pools and get buffers in both managers
        let pool1 = manager1.GetBufferPool 100 100
        let buffer1 = pool1.GetBuffer()
        pool1.ReturnBuffer(buffer1)
        
        let pool2 = manager2.GetBufferPool 100 100
        let buffer2 = pool2.GetBuffer()
        pool2.ReturnBuffer(buffer2)
        
        // Clear first manager
        manager1.ClearAll()
        
        // Get new buffers from both
        let buffer1b = pool1.GetBuffer()
        let buffer2b = pool2.GetBuffer()
        
        // First pool should create a new buffer, second should reuse
        Object.ReferenceEquals(buffer1, buffer1b) |> should equal false
        Object.ReferenceEquals(buffer2, buffer2b) |> should equal true

    [<Fact>]
    let ``PooledBuffer should return buffer when disposed`` () =
        use manager = new BufferPoolManager()
        let pool = manager.GetBufferPool 50 50
        
        // Create a pooled buffer (should create a new buffer)
        use pooledBuffer = new PooledBuffer(manager, 50, 50)
        
        // Verify one buffer created
        pool.Created |> should equal 1
        
        // Force dispose
        (pooledBuffer :> IDisposable).Dispose()
        
        // Get a new buffer - should be reused
        let buffer = pool.GetBuffer()
        
        pool.Reused |> should equal 1
            
    [<Fact>]
    let ``Using block should properly dispose PooledBuffer`` () =
        use manager = new BufferPoolManager()
        let pool = manager.GetBufferPool 50 50
        
        // Create a reference outside the using block to test against later
        let mutable bufferRef = null
        
        // Scope for pooled buffer
        do
            use pooledBuffer = createPooledBuffer manager 50 50
            bufferRef <- pooledBuffer.Buffer
            // End of this block will dispose pooledBuffer
        
        // Get another buffer after the pooled one is disposed
        let buffer = pool.GetBuffer()
        
        // Should be the same instance and stats should be correct
        pool.Created |> should equal 1
        pool.Reused |> should equal 1
        Object.ReferenceEquals(bufferRef, buffer) |> should equal true