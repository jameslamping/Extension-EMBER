using System;
using Xunit;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER.Tests
{
    public class BehaviorTests
    {
        // Anderson (1982) fuel models, loads in tons/acre converted to kg/m2 (1 t/ac = 0.2242 kg/m2)
        const double TonAc = 0.22417;

        static SurfaceFuel FM10()
        {
            var f = SurfaceFuel.Empty;
            f.W1h = 3.01 * TonAc; f.W10h = 2.0 * TonAc; f.W100h = 5.0 * TonAc; f.WWoody = 2.0 * TonAc; f.WHerb = 0;
            f.Sav1h = 2000; f.Sav10h = 109; f.Sav100h = 30; f.SavWoody = 1500;
            f.HeatContent = 18608; f.MoistureOfExtinction = 0.25;
            f.PackingRatio = 0.01727;   // depth 1.0 ft, total load 12.02 t/ac
            return f;
        }

        static SurfaceFuel FM1()
        {
            var f = SurfaceFuel.Empty;
            f.W1h = 0.74 * TonAc; f.Sav1h = 3500; f.HeatContent = 18608; f.MoistureOfExtinction = 0.12;
            f.PackingRatio = 0.00106;   // depth 1.0 ft
            return f;
        }

        static FuelMoistureState LowMoisture()
        {
            return new FuelMoistureState { M1h = 0.08, M10h = 0.08, M100h = 0.08, M1000h = 0.15, MHerb = 1.0, MWoody = 1.0, MDuff = 0.5, MFoliar = 1.0 };
        }

        [Fact]
        public void FuelModel10_MatchesAndersonTypicalBehavior()
        {
            // Anderson 1982: FM10, 5 mi/h midflame wind, 8 % dead moisture, 100 % live -> 7.9 ch/h, 4.8 ft flame
            var r = Rothermel.Compute(FM10(), LowMoisture(), 8.047, 0, 0, 0);
            Assert.True(r.Burnable);
            double rosChH = r.Ros_mMin / 0.3048 * 60.0 / 66.0;
            Assert.InRange(rosChH, 5.5, 10.5);
            Assert.InRange(r.FlameLength_m / 0.3048, 3.5, 6.5);
        }

        [Fact]
        public void FuelModel1_MatchesAndersonTypicalBehavior()
        {
            // Anderson 1982: FM1, 5 mi/h midflame, 8 % moisture -> 78 ch/h, 4 ft flame
            var r = Rothermel.Compute(FM1(), LowMoisture(), 8.047, 0, 0, 0);
            Assert.True(r.Burnable);
            double rosChH = r.Ros_mMin / 0.3048 * 60.0 / 66.0;
            Assert.InRange(rosChH, 55, 105);
            Assert.InRange(r.FlameLength_m / 0.3048, 2.8, 5.5);
        }

        [Fact]
        public void WetFuelDoesNotBurn()
        {
            var m = LowMoisture(); m.M1h = 0.30; m.M10h = 0.30; m.M100h = 0.30;
            var r = Rothermel.Compute(FM10(), m, 8.0, 0, 0, 0);
            Assert.False(r.Burnable);
        }

        [Fact]
        public void WindAndSlopeIncreaseSpreadAndSetHeading()
        {
            var calm = Rothermel.Compute(FM10(), LowMoisture(), 0, 0, 0, 0);
            var windy = Rothermel.Compute(FM10(), LowMoisture(), 20, 90, 0, 0);
            var upslope = Rothermel.Compute(FM10(), LowMoisture(), 0, 0, 30, 180); // aspect 180 -> upslope is 0 (north)
            Assert.True(windy.Ros_mMin > calm.Ros_mMin * 2);
            Assert.InRange(windy.HeadDirection_deg, 89, 91);
            Assert.True(upslope.Ros_mMin > calm.Ros_mMin);
            Assert.InRange(Math.Abs(Units.AngleDiff(upslope.HeadDirection_deg, 0)), 0, 1);
        }

        [Fact]
        public void FfmcToMoistureMatchesVanWagner()
        {
            Assert.InRange(FuelMoisture.FromFFMC(90) * 100, 10.7, 10.9);
            Assert.InRange(FuelMoisture.FromFFMC(0) * 100, 249, 251);
        }

        [Fact]
        public void CriticalSurfaceIntensityVanWagnerExample()
        {
            // CBH 5 m, FMC 100 %: (0.01 * 5 * (460 + 2590))^1.5 = 1883 kW/m
            Assert.InRange(CrownFire.CriticalSurfaceIntensity(5.0, 100.0), 1870, 1900);
        }

        [Fact]
        public void CrownFireClassesRespondToIntensityAndWind()
        {
            var canopy = new CanopyFuel { CanopyBaseHeight_m = 2.0, CanopyBulkDensity_kgm3 = 0.15, CanopyFuelLoad_kgm2 = 1.2, CanopyHeight_m = 25, CanopyCover = 0.8 };
            var calmSurf = Rothermel.Compute(FM10(), LowMoisture(), 1.0, 0, 0, 0);
            var calm = CrownFire.Evaluate(calmSurf, canopy, 100, 3.0, 8.0, true);
            var windySurf = Rothermel.Compute(FM10(), LowMoisture(), 15.0, 0, 0, 0);
            var windy = CrownFire.Evaluate(windySurf, canopy, 90, 40.0, 6.0, true);
            Assert.True((int)windy.CrownClass >= (int)calm.CrownClass);
            Assert.Equal(CrownFireClass.Active, windy.CrownClass);
            Assert.True(windy.FinalIntensity_kWm > windySurf.FirelineIntensity_kWm);
            Assert.InRange(windy.CrownFractionBurned, 0.9, 1.0);
        }

        [Fact]
        public void EllipseGeometry()
        {
            Assert.InRange(Ellipse.LengthToBreadth(0), 0.99, 1.01);
            double lb = Ellipse.LengthToBreadth(30);
            Assert.True(lb > 2 && lb <= 8);
            double e = Ellipse.Eccentricity(lb);
            Assert.InRange(Ellipse.DirectionalRos(10, e, 0), 9.99, 10.01);
            Assert.True(Ellipse.DirectionalRos(10, e, 180) < 3);
            Assert.InRange(Ellipse.DirectionalRos(10, e, 180), Ellipse.BackingRos(10, e) - 1e-9, Ellipse.BackingRos(10, e) + 1e-9);
        }

        [Fact]
        public void ScorchAndMortality()
        {
            double hs = Mortality.ScorchHeight(500, 0, 20);
            Assert.InRange(hs, 6.0, 9.0);
            Assert.InRange(Mortality.CrownVolumeScorchedPct(10, 5, 15), 74, 76);
            Assert.True(Mortality.MortalityProbability(0.2, 100) > 0.95);
            Assert.True(Mortality.MortalityProbability(3.0, 0) < 0.05);
            Assert.InRange(Mortality.BarkThicknessCm(8.27, 100, 100), 4.1, 4.2);
        }

        [Fact]
        public void FoliarMoistureHasSpringDip()
        {
            double june = FuelMoisture.ConiferFoliarMoisturePct(160, 47.8, -121.7, 0, 0.25);
            double sept = FuelMoisture.ConiferFoliarMoisturePct(250, 47.8, -121.7, 0, 0.25);
            Assert.True(june < sept);
            Assert.InRange(sept, 119, 121);
            double drought = FuelMoisture.ConiferFoliarMoisturePct(250, 47.8, -121.7, 1.0, 0.25);
            Assert.InRange(drought, 89, 91);
        }

        [Fact]
        public void ConsumptionFollowsMoisture()
        {
            var p = new ConsumptionParameters();
            var fuel = FM10();
            var dry = Consumption.Compute(p, fuel, 3.0, 2.0, LowMoisture(), 0.5, true);
            var wetState = LowMoisture(); wetState.M100h = 0.40; wetState.M1000h = 0.50; wetState.MDuff = 1.2;
            var wet = Consumption.Compute(p, fuel, 3.0, 2.0, wetState, 0.5, true);
            Assert.True(dry.Surface100h > wet.Surface100h);
            Assert.True(dry.Surface1000h > wet.Surface1000h);
            Assert.True(dry.Duff > wet.Duff);
            Assert.InRange(wet.Duff, 0, 0.01);
            Assert.InRange(dry.Canopy, 0.499, 0.501);
        }
    
        [Fact]
        public void CruzCrownFireProbabilityMatchesPaper()
        {
            // Cruz et al. (2004) Table 4: U10 10 km/h, FSG 6 m, EFFM 6 %, SFC 1-2 kg/m2 -> logit -0.296
            double p = CrownFire.CruzCrownFireProbability(10, 6, 6, 1.5);
            Assert.InRange(p, 0.42, 0.44);
            Assert.True(CrownFire.CruzCrownFireProbability(25, 3, 6, 2.5) > 0.99);
            Assert.True(CrownFire.CruzCrownFireProbability(5, 12, 15, 0.5) < 0.001);
        }
}
}
