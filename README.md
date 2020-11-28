# Goo2Prototype
Personal project to help me learn Unity's Data Oriented Technology Stack for the first time.

==11/28/2020==
The original "Goo!" was made by Tommy Refenes and myself. It used a lot of parallelism to become so fluid. But the simulation of the fluid was always limited by how many "blobs" of goo could be processed at once. It's now around fifteen years later, and I'm interested in reproducing the game in a modern engine to make use of the increase of processing power.

I should note that the aim of Goo's fluid simulation was never to be realistic, but instead to be very "playable". As a result, the forces between blobs are not always amazingly well optimized as I try to get the best game-feel, as opposed to the best use of the hardware.

My aim is to start with a functioning but unoptimized fake-fluid model that feels good (and is, ideally, a playable game, too). After this, I'll do a lot of profiling to see what optimisations are best. I'm fairly certain that sometimes it will be worth "skipping" optimisation steps for a particular volume of jobs, because sometimes the optimisation overhead is not worth it if you happen to only be comparing 2 blobs against one another.

There's probably going to be a lot of fun graph algorithms.

I'm unlikely to do too much on the graphics side, at first, but I'm hoping I'll be able to use the new unity particle systems as a way to present the Goo, or otherwise do some kind of screen space marching cubes meshification of the point cloud.
