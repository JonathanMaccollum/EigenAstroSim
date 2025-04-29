Let's design a new application to support our efforts.  
Using the following technology stack, let's create a desktop application that serves as a simulator for an overall guiding setup that performs the following goals:
* It implements an ASCOM Mount driver that allows other software to connect and send normal slew/guide/sync commands etc Our virtual mount should be an EQ mount that is properly polar aligned, but that a user can specify a slight offset.
* It implements an ASCOM camera driver that simulates a "guide camera" that allows guiding software to connect to and provide synthetic images based on a made up star field that we generate and expand on the fly.  
* In the future it would be neat to also implement a "main imaging camera" that also gets exposed by the software, but lets focus only on the guide camera for this phase of the design.
* It implements an ASCOM rotator driver that allows client applications to "rotate" the virtual camera, having a net field rotation impact on the stars in the field.
* It displays the current synthetic star field images in the UI
* It provides input controls to allow us to manually slew at different speeds just like a telescope mount would.
* It allows us to simulate seeing conditions at different layers of the atmosphere.
* It allows us to simulate tracking issues or cable snags that cause movement in RA or Dec
* It properly maintains our star field such that as the virtual telescope mount slews to/from a location the same synthetic stars show back up in the same spot.
* Slews and nudges properly allow stars to trail as the scope moves relative to the sky during moves. 
* We should try to accurately model how point light sources are accumulating on specific pixels spread across our virtual sensor.  During perfect seeing and tracking conditions these should model good high quality point sources with low FWHM.
* Stars are limited based on the telescope capabilities provided by the user.
* We should model our sky quality darkness such that our star field shows a few very bright stars, but also models some fainter stars that show when the sky darkens or when longer exposures are used.  
* We should try to include a cloud layer that interferes with the stars that are showing up in the resulting image when different types of clouds start to form.
* We should try and also introduce random satellites going through the frame on occasion, but this should be infrequent, perhaps allow the user to hit a button to generate one in our sky, but that disappears when it leaves the fov so to not add too much complexity
* We should try to generate synthetic noise that resembles the noise and banding typical in modern guide cameras
* We should support binning by the client, that properly bins our generated image after all other factors are accounted for when sending to the client application.
* We will not introduce a focuser or a filter wheel or camera cooler at this point.
Technology Stack:
* Language: F# using Functional Programming Best practices
* UI: WPF and MVVM
* Reactive Extensions for dotnet
* Dotnet 8
* ASCOM Platform

The goal of our new application will be to allow for the development of astrophotography based acquisition software such as our new Guider that uses Machine Learning, or troubleshoot other astrophotography softare using a virtual night sky.  In the future, it might be worth replacing the virtual star field with a star catalog data such as the catalogs used by ASTAP, but for now we'll work with a synthetic field.  The star field starts empty, and builds over time based on where the mount is pointing in our virtual sky. 

Create a new artifact containing the architectural design of our astro equipment simulator.