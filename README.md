# RealAntennas
KSP Mod to add better antenna / link calculations to CommNet.  Extends most CommNet classes.

First, thanks to TaxiService; CommNetConstellation (https://github.com/KSP-TaxiService/CommNetConstellation) was invaluable in getting started with modifying CommNet.

The primary driver for this mod is to replace the KSP notion of an individual antenna having a "range" as a singular value that presumably derives from its gain and its transmission power.  KSP's stock CommNet exposes a replacable RangeModel at its top level Scenario, but unfortunately the interface's parameters only contain the "range" doubles and the distance double.  Direct access to the CommNode object, and a method to control the object's class when it is created [in several different places] would have made this mod simpler.

RealAntennas implements a new interface in place of the RangeModel, which operates on extended CommNode objects in CommNetwork.  The new objects have a different version of the antenna info, with characteristics for antenna gain, transmit power, modulator, and receiver noise figure [among others].  It implements a typical link budget calculation:  RxPower = TxPower + TxGain - FreeSpacePathLoss + RxGain.  C/I or SNR = RxPower - Rx_NoiseFloor.

The current baseline implements a modulator for BPSK/QPSK/8PSK/QAM varieties.  The C/I (or Es/No) is compared against minimum values to select the highest order modulation.

Data rate is calculated as sample rate * bits/sample.  Bandwidth is given as sample rate / spectral efficiency, which is fairly reasonable for digital communications.  

Initial simplifications: the noise temperature of any antenna is fixed at 290K (as if it's always an omni on Earth).  The only path loss is free-space (ie no atmospheric, or edge-diffraction).  There is no pointing loss or enforced antenna directionality ala RT.  We are dropping the idea of Coding Gain (increasing signal via time integration) for now.

Frequency impacts path loss, and antennas that are not "close enough" (10% for now) in frequency cannot communicate.  Bandwidth impacts noise power as expected.  A given pair of CommNodes will test all possible pairs of antennas to select the best pairing for the link *in each direction*.  Modulator pairs will always find the best modulation scheme they can both handle.  CommNet links are ALMOST asymmetric: the calculations are there, but there is only a single link cost score, and I do not plan to rewrite the CommNet network graph construction/pathfinding.

Antenna selection is deferred until TryConnect(), instead of selecting in UpdateComm() like stock.  This way we attempt all pairwise antenna choices between two nodes.  We should pay attention to runtime costs for this.  The best link is defined as the one that produces the highest data rate.  Other paradigms are possible, and this determination could be made per-direction.  (Idea: implement "best performance" and "lowest power" link selection.)

We nullify the stock RangeModel queries, especially now that we are deferring antenna selection until TryConnect(), so we need to get there for nearly all node pairs.  Our RangeModel remains a home for RSSI, path loss, noise floor and noise temperature calculations.

We create from scratch CommNetHome and CommNetBody objects rather than replacing CommNet stock's.  We borrowed RemoteTech's ConfigNode structure.  We modified these primarily to get access to the methods that created CommNodes so we could replace them with our custom class.

We now have a basic link budget calculation, direction-specific data transmission rates, and variable modulation schemes to maximize data rate for the current SNR.  I don't think I have much talent for UI, so that will need some help.

Reference materiel:

https://forum.kerbalspaceprogram.com/index.php?/topic/156251-commnet-notes-for-modders/
https://kerbalspaceprogram.com/api/namespace_comm_net.html

Noise:

http://literature.cdn.keysight.com/litweb/pdf/5952-8255E.pdf

http://www.delmarnorth.com/microwave/requirements/satellite_noise.pdf

https://www.itu.int/dms_pubrec/itu-r/rec/p/R-REC-P.372-7-200102-S!!PDF-E.pdf

https://en.wikipedia.org/wiki/Johnson%E2%80%93Nyquist_noise

https://en.wikipedia.org/wiki/Noise_temperature


Antenna BW:

http://www.cs.binghamton.edu/~vinkolar/directional/Direct_antennas.pdf

http://www.tscm.com/antennas.pdf

http://www.satsig.net/pointing/antenna-beamwidth-calculator.htm

BW/Symbol Rate:

https://dsp.stackexchange.com/questions/41983/relation-between-bandwidth-and-baud-rate-for-8-psk

