MediaBrowser.IsoMounting
========================

Contains Media Browser's iso mounting solution, powered by [Pismo File Mount.](http://www.pismotechnic.com/ "Pismo File Mount")

This implements two core interfaces, IIsoManager, and IIsoMount.

The manager class can be used to create a mount, and also determine if the mounter is capable of mounting a given file.

IIsoMount then represents a mount, which will be unmounted on disposal.