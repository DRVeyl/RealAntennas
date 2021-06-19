using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealAntennas.MapUI
{
    public class Settings
    {
        [Persistent] public float coneOpacity = 1;
        [Persistent] public int coneCircles = 4;
        [Persistent] public RACommNetUI.DrawConesMode drawConesMode = RACommNetUI.DrawConesMode.Cone3D;
        [Persistent] public RACommNetUI.RadioPerspective radioPerspective = RACommNetUI.RadioPerspective.Transmit;
        [Persistent] public bool drawTarget = false;
        [Persistent] public bool drawCone3 = true;
        [Persistent] public bool drawCone10 = true;
        [Persistent] public float lineScaleWidth = 2.5f;
    }
}
