using UnityEngine;

//The simulation's single source of time. Everything that moves on a schedule — orbits,
//planet rotation, day/night, and eventually NPC routines and the economy — reads this
//clock rather than Time.time, so the whole universe can be paused, accelerated or
//warped coherently.
//Time is kept in DOUBLE days per Law 4: floats lose sub-second precision after a few
//in-game years, which would make orbits jitter
[ExecuteAlways]
public class SFUniverseClock : MonoBehaviour
{
    //Simulated days elapsed since epoch
    [SerializeField] private double currentDay = 0.0;

    //How fast the universe runs. The scale profile supplies the baseline; this multiplies
    //it, so the player can warp time without changing the world's calibration
    [Range(0.0f, 1000.0f)]
    public float timeScale = 1.0f;

    public bool paused = false;

    //Baseline rate, normally taken from the system's scale profile
    public double daysPerSecond = 0.02;

    public double CurrentDay { get { return currentDay; } }

    //Years are useful for reporting and for anything on a seasonal cycle
    public double CurrentYear { get { return currentDay / 365.256; } }

    private static SFUniverseClock instance;

    //Any system can ask for the clock; one is created on demand if the scene has none
    public static SFUniverseClock Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SFUniverseClock>();

                if (instance == null && Application.isPlaying)
                {
                    GameObject clockObject = new GameObject("Universe Clock");
                    instance = clockObject.AddComponent<SFUniverseClock>();
                }
            }

            return instance;
        }
    }

    private void OnEnable()
    {
        instance = this;
    }

    private void Update()
    {
        if (!Application.isPlaying || paused)
            return;

        currentDay += Time.deltaTime * daysPerSecond * timeScale;
    }

    //Jump the simulation — for fast travel, sleeping, or scrubbing the system map
    public void AdvanceDays(double _days)
    {
        currentDay += _days;
    }

    public void SetDay(double _day)
    {
        currentDay = _day;
    }

    //Formatted for UI: "Year 3, Day 142, 06:30"
    public string GetDateString()
    {
        double totalDays = currentDay;
        int year = (int)(totalDays / 365.256) + 1;
        double dayOfYear = totalDays % 365.256;
        int day = (int)dayOfYear + 1;
        double hours = (dayOfYear - (int)dayOfYear) * 24.0;
        int hour = (int)hours;
        int minute = (int)((hours - hour) * 60.0);

        return "Year " + year + ", Day " + day + ", " + hour.ToString("00") + ":" + minute.ToString("00");
    }
}
