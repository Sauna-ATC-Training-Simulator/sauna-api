using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SaunaSim.Core.Data;
using SaunaSim.Core.Simulator.Aircraft.Control.FMS;

namespace SaunaSim.Core.Simulator.Aircraft.Control
{
    public partial class AircraftControl
    {
        private ILateralControlInstruction _currentLateralMode;
        private ILateralControlInstruction _armedLateralMode;
        private IVerticalControlInstruction _currentVerticalMode;
        private List<IVerticalControlInstruction> _armedVerticalModes;
        private object _armedVerticalModesLock;
        private AircraftFms _fms;

        public AircraftControl(ILateralControlInstruction lateralInstruction, IVerticalControlInstruction verticalInstruction)
        {
            _currentLateralMode = lateralInstruction;
            _currentVerticalMode = verticalInstruction;
            _armedVerticalModesLock = new object();
            _fms = new AircraftFms();

            lock (_armedVerticalModesLock)
            {
                _armedVerticalModes = new List<IVerticalControlInstruction>();
            }
        }

        public AircraftControl() : this(new HeadingHoldInstruction(0), new AltitudeHoldInstruction(10000)) { }

        public AircraftFms FMS => _fms;

        public ILateralControlInstruction CurrentLateralInstruction
        {
            get => _currentLateralMode;
            set => _currentLateralMode = value;
        }

        public ILateralControlInstruction ArmedLateralInstruction
        {
            get => _armedLateralMode;
            set
            {
                if (value == null || _armedLateralMode == null || _armedLateralMode.Type != value.Type)
                {
                    _armedLateralMode = value;
                }
            }
        }

        public IVerticalControlInstruction CurrentVerticalInstruction
        {
            get => _currentVerticalMode;
            set => _currentVerticalMode = value;
        }

        public List<IVerticalControlInstruction> ArmedVerticalInstructions
        {
            get
            {
                lock (_armedVerticalModesLock)
                {
                    return _armedVerticalModes.ToList();
                }
            }
        }

        public bool AddArmedVerticalInstruction(IVerticalControlInstruction instr)
        {
            if (_currentVerticalMode.Type == VerticalControlMode.GLIDESLOPE)
            {
                return false;
            }

            lock (_armedVerticalModesLock)
            {
                List<int> deletionList = new List<int>();

                int i = 0;
                foreach (IVerticalControlInstruction elem in _armedVerticalModes)
                {
                    if (elem.Type == instr.Type)
                    {
                        // Delete instruction
                        deletionList.Add(i);
                    }
                    i++;
                }

                foreach (int index in deletionList)
                {
                    _armedVerticalModes.RemoveAt(index);
                }

                _armedVerticalModes.Add(instr);
            }
            return true;
        }

        public void UpdatePosition(ref AircraftPosition position, int posCalcInterval)
        {
            // Check if we should activate armed instructions
            if (ArmedLateralInstruction != null && ArmedLateralInstruction.ShouldActivateInstruction(position, _fms, posCalcInterval))
            {
                CurrentLateralInstruction = ArmedLateralInstruction;
                ArmedLateralInstruction = null;
            }

            lock (_armedVerticalModesLock)
            {
                foreach (IVerticalControlInstruction armedInstr in _armedVerticalModes)
                {
                    if (armedInstr.ShouldActivateInstruction(position, _fms, posCalcInterval))
                    {
                        CurrentVerticalInstruction = armedInstr;
                        _armedVerticalModes.Remove(armedInstr);

                        // If glideslope, clear the armed list
                        if (armedInstr.Type == VerticalControlMode.GLIDESLOPE)
                        {
                            _armedVerticalModes.Clear();
                        }
                        break;
                    }
                }
            }

            // Update position
            CurrentLateralInstruction.UpdatePosition(ref position, ref _fms, posCalcInterval);
            CurrentVerticalInstruction.UpdatePosition(ref position, ref _fms, posCalcInterval);

            // Recalculate values
            position.UpdateGribPoint();
        }
    }
}
