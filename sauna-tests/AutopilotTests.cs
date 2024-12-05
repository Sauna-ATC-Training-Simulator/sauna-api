using System;
using AviationCalcUtilNet.Geo;
using AviationCalcUtilNet.GeoTools;
using AviationCalcUtilNet.MathTools;
using AviationCalcUtilNet.Units;
using NUnit.Framework.Constraints;
using SaunaSim.Core.Simulator.Aircraft.Autopilot.Controller;
using SaunaSim.Core.Simulator.Aircraft.Performance;

namespace sauna_tests
{
    /*
    public class AutopilotTests
    {
        private PerfData _perfData;

        [SetUp]
        public void Setup()
        {
            _perfData = PerfDataHandler.LookupForAircraft("A320");
        }

        [Test]
        public void HdgTest1()
        {
            Angle demandedRoll = AutopilotUtil.CalculateDemandedRollForTurn(Angle.FromDegrees(30), Angle.FromDegrees(0), Velocity.FromKnots(250), 100);
            Assert.That(Math.Abs(demandedRoll.Degrees - 25), Is.LessThan(0.1));
        }

        [Test]
        public void FlchTest1()
        {
            double speedDelta = 10; // 10 kts fast
            double targetSpd = 250;
            double densAlt = 10000;
            double massKg = _perfData.MTOW_kg;

            Angle maxPitch = AutopilotUtil.PITCH_LIMIT_MAX;
            Angle minPitch = AutopilotUtil.PITCH_LIMIT_MIN;
            double zeroVsPitch = PerfDataHandler.GetRequiredPitchForVs(_perfData, 0, targetSpd + speedDelta, densAlt, massKg, 0, 0);
            double zeroAccelPitch = PerfDataHandler.GetRequiredPitchForThrust(_perfData, 0, 0, targetSpd, densAlt, massKg, 0, 0);
            var pitchToVsFunc = (Angle pitch) => Velocity.FromFeetPerMinute(PerfDataHandler.CalculatePerformance(
                _perfData,
                pitch.Degrees,
                0,
                targetSpd + speedDelta,
                densAlt,
                massKg,
                0,
                0).vs);
            Angle demandedPitch = AutopilotUtil.CalculateDemandedPitchForSpeed(Velocity.FromKnots(speedDelta), Angle.FromDegrees(10), Angle.FromDegrees(zeroAccelPitch), maxPitch, minPitch, (Angle pitch) => Acceleration.FromKnotsPerSecond(PerfDataHandler.CalculatePerformance(_perfData, pitch.Degrees, 0, targetSpd + speedDelta, densAlt, massKg, 0, 0).accelFwd), pitchToVsFunc, 10);
            Assert.That(demandedPitch.Degrees, Is.EqualTo(zeroAccelPitch));
        }

        [Test]
        public void ShouldAselTest1()
        {
            double altDelta = 100;
            double ias = 250;
            double densAlt = 10000;
            double massKg = _perfData.MTOW_kg;

            // Calculate required ASEL pitch
            double zeroVsPitch = PerfDataHandler.GetRequiredPitchForVs(_perfData, 0, ias, densAlt, massKg, 0, 0);
            double idlePitch = PerfDataHandler.GetRequiredPitchForThrust(_perfData, 0, 0, ias, densAlt, massKg, 0, 0);
            double maxPitch = PerfDataHandler.GetRequiredPitchForThrust(_perfData, 1, 0, ias, densAlt, massKg, 0, 0);
            Angle pitchTarget = AutopilotUtil.CalculateDemandedPitchForAltitude(
                Length.FromFeet(altDelta),
                Angle.FromDegrees(idlePitch),
                Angle.FromDegrees(zeroVsPitch),
                Angle.FromDegrees(idlePitch),
                Angle.FromDegrees(maxPitch),
                (pitch) => Velocity.FromFeetPerMinute(PerfDataHandler.CalculatePerformance(_perfData, pitch.Degrees, 0, ias, densAlt, massKg, 0, 0).vs),
                100
            );

            Assert.That(Math.Abs(zeroVsPitch - pitchTarget.Degrees), Is.LessThan(double.Epsilon));
        }

        [Test]
        public void LnavTest1()
        {
            double gs = 250;
            (var demandedRoll, var demandedTrack) = AutopilotUtil.CalculateDemandedRollForNav(Length.FromMeters(-10000), Bearing.FromDegrees(0), Bearing.FromDegrees(0), Length.FromMeters(0), Angle.FromDegrees(0), Velocity.FromKnots(gs), 100);
            Assert.Multiple(() =>
            {
                Assert.That(Math.Abs(demandedRoll.Degrees - 25), Is.LessThan(0.1));
                Assert.That(Math.Abs(demandedTrack.Degrees - 45), Is.LessThan(0.1));
            });
        }

        [Test]
        public void LnavTest2()
        {
            double gs = 250;
            (var demandedRoll, var demandedTrack) = AutopilotUtil.CalculateDemandedRollForNav(Length.FromMeters(-10), Bearing.FromDegrees(45), Bearing.FromDegrees(0), Length.FromMeters(0), Angle.FromDegrees(0), Velocity.FromKnots(gs), 100);
            Assert.Multiple(() =>
            {
                Assert.That(Math.Abs(demandedRoll.Degrees - -25), Is.LessThan(0.1));
                Assert.That(Math.Abs(demandedTrack.Degrees - 0), Is.LessThan(0.1));
            });
        }

        [Test]
        public void LnavTest3()
        {
            var courseDeviation = Length.FromMeters(540.4165452451924);
            var curTrueTrack = Bearing.FromDegrees(269.222103021776);
            var courseTrueTrack = Bearing.FromDegrees(314.11948759322655);
            var courseTurnRadius = Length.FromMeters(0);
            var curRoll = Angle.FromRadians(0.03214721801265882);
            var groundSpeed = Velocity.FromMetersPerSecond(149.4047937055521);
            int intervalMs = 25;

            (var demandedRoll, var demandedTrack) = AutopilotUtil.CalculateDemandedRollForNav(courseDeviation, curTrueTrack, courseTrueTrack, courseTurnRadius, curRoll, groundSpeed, intervalMs);

            Assert.Multiple(() =>
            {
                Assert.That(Math.Abs(demandedRoll.Degrees - 25), Is.LessThan(0.1));
                Assert.That(Math.Abs(demandedTrack.Degrees - courseTrueTrack.Degrees), Is.LessThan(0.1));
            });
        }
    }
    */
}

