using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SFAccrete
{
    private bool            DUST_LEFT;

    private SFDust          CURRENT_DUST_BAND = new SFDust();
    private List<SFDust>    DUST_LIST         = new List<SFDust>();
    private List<SFNuclei>  NUCLEI_LIST       = new List<SFNuclei>();

    public int NUCLEI_USED;

    public void SetInitialConditions(float _innerDustLimit, float _outerDustLimit)
    {
        DUST_LEFT = true;

        SFDust temp = new SFDust();
        temp.GasPresent = true;
        temp.DustPresent = true;
        temp.InnerEdge = _innerDustLimit;
        temp.OuterEdge = _outerDustLimit;

        DUST_LIST.Add(temp);
    }

    private bool DustAvailable(float _a)
    {
        bool dustAvailable = false;

        for (int i = 0; i < DUST_LIST.Count; i++)
        {
            if (_a >= DUST_LIST[i].InnerEdge && _a <= DUST_LIST[i].OuterEdge && DUST_LIST[i].DustPresent)
            {
                DUST_LIST[i].Index = i;
                CURRENT_DUST_BAND = DUST_LIST[i];
                dustAvailable = true;
            }
        }
        return dustAvailable;
    }

    private void CollectDust(SFNuclei _protoPlanet, float _stellarLuminosityRatio)
    {
        float t1, t2, t3,
              width,
              volume,
              bandWidth,
              gasDensity = 0.0f,
              tempDensity,
              massDensity;

        _protoPlanet.R_p = _protoPlanet.Axis * (1 - _protoPlanet.Eccen);
        _protoPlanet.R_a = _protoPlanet.Axis * (1 + _protoPlanet.Eccen);

        _protoPlanet.MassUnit = Mathf.Pow(_protoPlanet.Mass / (1.0f + _protoPlanet.Mass), 1.0f / 4.0f);

        _protoPlanet.X_p = SFUtilities.CalculateInnerEffectLimit(_protoPlanet.Axis, _protoPlanet.Eccen, _protoPlanet.MassUnit);

        if (_protoPlanet.X_p >= CURRENT_DUST_BAND.InnerEdge)
        {
            _protoPlanet.S_p = _protoPlanet.X_p;
        }
        else
        {
            _protoPlanet.S_p = CURRENT_DUST_BAND.InnerEdge;
        }
    
        _protoPlanet.X_a = SFUtilities.CalculateOuterEffectLimit(_protoPlanet.Axis, _protoPlanet.Eccen, _protoPlanet.MassUnit);
        
        if (_protoPlanet.X_a <= CURRENT_DUST_BAND.OuterEdge)
        {
            _protoPlanet.S_a = _protoPlanet.X_a;
        }
        else
        {
            _protoPlanet.S_a = CURRENT_DUST_BAND.OuterEdge;
        }

        if (_protoPlanet.X_p < 0.0f)
            _protoPlanet.X_p = 0.0f;

        if (CURRENT_DUST_BAND.DustPresent == false)
            tempDensity = 0.0f;
        else
            tempDensity = _protoPlanet.DustDensity;

        if ((_protoPlanet.Mass < _protoPlanet.CritMass) || CURRENT_DUST_BAND.GasPresent == false)
            massDensity = tempDensity;
        else
        {
            massDensity = SFUtilities.CalculateDustAndGasDensity(_protoPlanet.Axis, _stellarLuminosityRatio, _protoPlanet.CritMass, _protoPlanet.Mass);
            gasDensity = massDensity - tempDensity;
        }

        bandWidth = _protoPlanet.X_a - _protoPlanet.X_p;

        t1 = _protoPlanet.X_a - CURRENT_DUST_BAND.OuterEdge;
        if (t1 < 0.0f)
            t1 = 0.0f;
        width = bandWidth - t1;

        t2 = CURRENT_DUST_BAND.InnerEdge - _protoPlanet.X_p;
        if (t2 < 0.0f)
            t2 = 0.0f;
        width = width - t2;

        t3 = 4.0f * Mathf.PI * Mathf.Pow(_protoPlanet.Axis, 2.0f) * _protoPlanet.MassUnit * (1.0f - _protoPlanet.Eccen * (t1 - t2) / bandWidth);

        volume = t3 * width;

        _protoPlanet.Mass       = volume * massDensity;
        _protoPlanet.GasMass    = volume * gasDensity;
        _protoPlanet.DustMass   = _protoPlanet.Mass - _protoPlanet.GasMass;
    }

    private void UpdateDustList(float _innerEffectLimit, float _outerEffectLimit, float _mass, float _critMass, float _innerPlanetBoundary, float _outerPlanetBoundary)
    {
        bool gas;

        SFDust temp1 = new SFDust();    //Middle
        SFDust temp2 = new SFDust();    //Left
        SFDust temp3 = new SFDust();    //Right

        temp1 = CURRENT_DUST_BAND;

        //Debug.Log("Removing dust band " + CURRENT_DUST_BAND.Index);
        DUST_LIST.RemoveAt(CURRENT_DUST_BAND.Index);

        DUST_LEFT = false;

        if (_mass > _critMass)
            gas = false;
        else
            gas = true;

        if ((temp1.InnerEdge < _innerEffectLimit) && (temp1.OuterEdge > _outerEffectLimit))
        {
            //Debug.Log("Case 1");

            //Left...
            temp2.InnerEdge     = temp1.InnerEdge;
            temp2.OuterEdge     = _innerEffectLimit;
            temp2.GasPresent    = true;
            temp2.DustPresent   = true;

            DUST_LIST.Add(temp2);

            //Middle, this is where the planet is...
            temp1.InnerEdge     = _innerEffectLimit;
            //Before we overwrite...
            temp3.OuterEdge     = temp1.OuterEdge;
            //Now overwriting...
            temp1.OuterEdge     = _outerEffectLimit;
            temp1.HasPlanet     = true;
            temp1.GasPresent    = gas;
            temp1.DustPresent   = false;

            DUST_LIST.Add(temp1);

            //Right...
            temp3.GasPresent    = true;
            temp3.DustPresent   = true;
            temp3.InnerEdge     = _outerEffectLimit;

            DUST_LIST.Add(temp3);
        }
        else if ((temp1.InnerEdge < _outerEffectLimit) && (temp1.OuterEdge > _outerEffectLimit))
        {
            //Debug.Log("Case 2");

            //Left, this is where the planet is...
            temp2.InnerEdge     = _innerEffectLimit;
            temp2.OuterEdge     = _outerEffectLimit;
            temp2.HasPlanet     = true;
            temp2.GasPresent    = gas;
            temp2.DustPresent   = false;

            DUST_LIST.Add(temp2);

            //Right...
            temp3.InnerEdge     = _outerEffectLimit;
            temp3.OuterEdge     = temp1.OuterEdge;
            temp3.GasPresent    = true;
            temp3.DustPresent   = true;

            DUST_LIST.Add(temp3);
        }
        else if ((temp1.InnerEdge < _innerEffectLimit) && (temp1.OuterEdge > _innerEffectLimit))
        {
            //Debug.Log("Case 3");

            //Left
            temp2.InnerEdge     = temp1.InnerEdge;
            temp2.OuterEdge     = _innerEffectLimit;
            temp2.GasPresent    = true;
            temp2.DustPresent   = true;

            DUST_LIST.Add(temp2);

            //Right, this is where the planet is...
            temp3.InnerEdge     = _innerEffectLimit;
            temp3.OuterEdge     = temp1.OuterEdge;
            temp3.HasPlanet     = true;
            temp3.GasPresent    = gas;
            temp3.DustPresent   = false;

            DUST_LIST.Add(temp3);
        }
        else if ((temp1.InnerEdge >= _innerEffectLimit) && (temp1.OuterEdge <= _outerEffectLimit))
        {
            //Debug.Log("Case 4");
            temp1.InnerEdge     = _innerEffectLimit;
            temp2.InnerEdge     = _outerEffectLimit;
            temp1.GasPresent    = gas;
            temp1.DustPresent   = false;

            DUST_LIST.Add(temp1);
        }
        else if ((temp1.OuterEdge < _innerEffectLimit) || (temp1.InnerEdge > _outerEffectLimit))
        {
            //Debug.Log("Case 5");
            temp1.HasPlanet     = true;
            temp1.GasPresent    = gas;
            temp1.DustPresent   = false;

            DUST_LIST.Add(temp1);
        }

        DUST_LIST.Sort();

        for (int i = 0; i < DUST_LIST.Count; i++)
        {
            if (DUST_LIST[i].DustPresent && (DUST_LIST[i].OuterEdge >= _innerPlanetBoundary) && (DUST_LIST[i].InnerEdge <= _outerPlanetBoundary))
                DUST_LEFT = true;
        }
    }

    private void AccreteDust(SFNuclei _protoPlanet, float _stellarLuminosityRatio)
    {
        float newMass = _protoPlanet.Mass,
              prevMass;

        do
        {
            prevMass = newMass;
            CollectDust(_protoPlanet, _stellarLuminosityRatio);
            newMass = _protoPlanet.Mass;
        }
        while (!((newMass - prevMass) < (0.0001 * prevMass)));

        //UpdateDustList(_protoPlanet.S_p, _protoPlanet.S_a, _protoPlanet.Mass, _protoPlanet.CritMass);
    }

    private void CoalescePlanetesimals(SFNuclei _protoPlanet, float _stellarLuminosityRatio)
    {
        for (int i = 0; i < NUCLEI_LIST.Count; i++)
        {
            float t1,
                  t2,
                  t3,
                  t4,
                  d1,
                  d2,
                  a_new,
                  m_new, m_g_new, m_d_new,
                  e_new;

            t1 = NUCLEI_LIST[i].Axis - _protoPlanet.Axis;

            if (t1 > 0.0f)
            {
                d1 = (_protoPlanet.Axis * (1.0f + _protoPlanet.Eccen) * (1.0f + NUCLEI_LIST[i].MassUnit)) - _protoPlanet.Axis;

                //NUCLEI_LIST[i].MassUnit = Mathf.Pow(NUCLEI_LIST[i].Mass / (1.0f + NUCLEI_LIST[i].Mass), 1.0f / 4.0f);
                d2 = NUCLEI_LIST[i].Axis - (NUCLEI_LIST[i].Axis * (1.0f - NUCLEI_LIST[i].Eccen) * (1.0f - NUCLEI_LIST[i].MassUnit));
            }
            else
            {
                d1 = _protoPlanet.Axis - (_protoPlanet.Axis * (1.0f - _protoPlanet.Eccen) * (1.0f - NUCLEI_LIST[i].MassUnit));

                //NUCLEI_LIST[i].MassUnit = Mathf.Pow(NUCLEI_LIST[i].Mass / (1.0f + NUCLEI_LIST[i].Mass), 1.0f / 4.0f);
                d2 = (NUCLEI_LIST[i].Axis * (1.0f + NUCLEI_LIST[i].Eccen) * (1.0f * NUCLEI_LIST[i].MassUnit)) - NUCLEI_LIST[i].Axis;
            }

            if ((Mathf.Abs(t1) <= Mathf.Abs(d1)) || (Mathf.Abs(t1) <= Mathf.Abs(d2)))
            {
                a_new = (NUCLEI_LIST[i].Mass + _protoPlanet.Mass) / ((NUCLEI_LIST[i].Mass / NUCLEI_LIST[i].Axis) + (_protoPlanet.Mass / _protoPlanet.Axis));

                t2 = _protoPlanet.Mass * Mathf.Sqrt(_protoPlanet.Axis) * Mathf.Sqrt(1.0f - Mathf.Pow(_protoPlanet.Eccen, 2.0f));
                t3 = NUCLEI_LIST[i].Mass * Mathf.Sqrt(NUCLEI_LIST[i].Axis) * Mathf.Sqrt(1.0f - Mathf.Pow(NUCLEI_LIST[i].Eccen, 2.0f));
                t4 = (_protoPlanet.Mass + NUCLEI_LIST[i].Mass) * Mathf.Sqrt(a_new);

                e_new = Mathf.Sqrt(Mathf.Abs(1.0f - Mathf.Pow((t2 + t3) / t4, 2.0f)));
                m_new = NUCLEI_LIST[i].Mass + _protoPlanet.Mass;

                _protoPlanet.Mass       = m_new;
                _protoPlanet.Axis       = a_new;
                _protoPlanet.Eccen      = e_new;

                AccreteDust(_protoPlanet, _stellarLuminosityRatio);

                m_g_new = NUCLEI_LIST[i].GasMass + _protoPlanet.GasMass;
                m_d_new = NUCLEI_LIST[i].DustMass + _protoPlanet.DustMass;

                _protoPlanet.GasMass    = m_g_new;
                _protoPlanet.DustMass   = m_d_new;

                if (m_new > _protoPlanet.CritMass)
                    _protoPlanet.GasGiant = true;

                //Debug.Log("Coalescene of current protoplanet and nuclei " + i);

                NUCLEI_LIST.RemoveAt(i);

                //Debug.Log("New axis: " + a_new + " / New Eccen: " + e_new);
            }
        }
    }

    public List<SFNuclei> DistributePlanetaryMasses(float _stellarMassRatio, float _stellarLuminosityRatio, float _outerPlanetBoundary)
    {
        //float[] a = new float[] { 22.8f, 43.1f, 33.0f, 9.29f, 2.02f, 2.97f, 13.55f, 6.43f, 0.421f, 0.691f, 1.40f, .975f, 14.0f, 14.4f};
        //float[] e = new float[] { 0.149f, 0.112f, 0.132f, 0.007f, 0.092f, 0.408f, 0.016f, 0.229f, 0.269f, 0.093f, 0.125f, 0.209f, 0.023f, 0.003f };

        NUCLEI_USED = 0;

        while (DUST_LEFT)
        {
            NUCLEI_USED++;

            SFNuclei protoPlanet = new SFNuclei();

            float innerPlanetBoundary = SFUtilities.CalculateInnerPlanetBoundary(_stellarMassRatio),
                  outerPlanetBoundary;

            if (_outerPlanetBoundary == 0.0f)
                outerPlanetBoundary = SFUtilities.CalculateOuterPlanetBoundary(_stellarMassRatio);
            else
                outerPlanetBoundary = _outerPlanetBoundary;

            protoPlanet.Axis        = Random.Range(innerPlanetBoundary, outerPlanetBoundary);
            //protoPlanet.Axis = a[i];
            protoPlanet.Mass = SFConstants.PROTOPLANET_MASS;
            protoPlanet.S_p = 0.0f;
            protoPlanet.S_a = 0.0f;
            protoPlanet.X_p = 0.0f;
            protoPlanet.X_a = 0.0f;
            protoPlanet.Eccen       = SFUtilities.CalculateEccentricity();
            //protoPlanet.Eccen = e[i];
            protoPlanet.GasMass = 0.0f;
            protoPlanet.DustMass = 0.0f;
            protoPlanet.CritMass = SFUtilities.CalculateCriticalMass(protoPlanet.Axis, protoPlanet.Eccen, _stellarLuminosityRatio);
            protoPlanet.DustDensity = SFUtilities.CalculateDustDensity(protoPlanet.Axis, _stellarLuminosityRatio);

            //Debug.Log("--------------------------------------------------");
            //Debug.Log("Checking " + protoPlanet.Axis.ToString("0.00") + " AU...");

            if (DustAvailable(protoPlanet.Axis))
            {
                //Debug.Log("Injecting protoplanet at " + protoPlanet.Axis.ToString("0.00") + " AU.");

                AccreteDust(protoPlanet, _stellarLuminosityRatio);

                protoPlanet.DustMass += SFConstants.PROTOPLANET_MASS;

                if (protoPlanet.Mass > SFConstants.PROTOPLANET_MASS)
                {
                    //Debug.Log("Checking for coalescence...");
                    CoalescePlanetesimals(protoPlanet, _stellarLuminosityRatio);
                }
                else
                {
                    //Debug.Log("...failed due to large neighbor...");
                }

                if (protoPlanet.Mass >= protoPlanet.CritMass)
                    protoPlanet.GasGiant = true;

                UpdateDustList(protoPlanet.S_p, protoPlanet.S_a, protoPlanet.Mass, protoPlanet.CritMass, innerPlanetBoundary, outerPlanetBoundary);

                NUCLEI_LIST.Add(protoPlanet);
                NUCLEI_LIST.Sort();
            }
            else
            {
                //Debug.Log("Failed!");
            }

            //Debug.Log(DUST_LEFT);
        }

        return NUCLEI_LIST;
    }
}
