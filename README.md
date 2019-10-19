# RealAntennas
KSP Mod to add better antenna / link calculations to CommNet.  Extends most CommNet classes.

The primary driver for this mod is to replace the KSP notion of an individual antenna having a "range" as a singular value that presumably derives from its gain and its transmission power.  KSP's stock CommNet doesn't expose enough to change this behavior without replacing a few layers of classes.

The RealAntennas PartModule and its changes inside of CommNet implements a more detailed view of antenna & comms characteristics, such as antenna gain and transmit power; RF frequency, bandwidth and free-space path loss; some basic receiver noise modeling; and some elements from information theory (different modulation and encoding schemes, corresponding requirements for SNR (more precisely, Eb/N0), ultimately to provide variable data rates based on signal quality).

For those more familiar with the topic, it implements a typical link budget calculation:  RxPower = TxPower + TxGain - FreeSpacePathLoss + RxGain.  C/I or SNR = RxPower - Rx_NoiseFloor, with a variable threshold based on the modulation and encoding.

The current baseline implements a modulator for BPSK/QPSK/8PSK/QAM varieties.  The C/I (or Es/No) is compared against minimum values to select the highest order modulation.  Currently, there is only a model for digital communications.

Nodes (Vessels and ground stations) can have multiple antennas, each configured for a different band -- or even multiple antennas on the same band.  At most one link is established between any pair of nodes, based on the best individual pair of antennas *in each direction*.  Unlike stock, there is no combining of multiple antennas into a single unit, but you can  achieve asymmetric links where the data rate in one direction is significantly different from the other.

I've spent a fair amount of time with the Unity Profiler to minimize any GC or runtime issues in RealAntennas.  Please let me know if you encounter any performance-related issues that are tied to RA -- particularly if there is a noticable performance hit between RA and stock CommNet.
