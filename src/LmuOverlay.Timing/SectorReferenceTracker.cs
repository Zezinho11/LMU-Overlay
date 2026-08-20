using System.Diagnostics;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class SectorReferenceTracker
{
    private static readonly long ComparisonDurationTicks = Stopwatch.Frequency * 4;
    private readonly double[] _references = new double[3];
    private readonly double[] _persistentReferences = new double[3];
    private readonly double[] _lapCandidates = new double[3];
    private readonly double[] _currentLapSectors = new double[3];
    private readonly SectorReferenceOrigin[] _origins = new SectorReferenceOrigin[3];
    private readonly int[] _referenceLap = [-1, -1, -1];
    private readonly int[] _suppressReferenceOnLap = [-1, -1, -1];
    private readonly bool[] _contaminated = new bool[3];
    private string _sessionKey = string.Empty;
    private int _lastLapNumber = -1;
    private uint _lastScoringSequence;
    private int? _currentSector;
    private PendingSector? _pending;
    private double _lastLapElapsedSeconds;
    private double _lapSector1Seconds;
    private double _lapSector2Seconds;
    private bool _lapIsOutLap;
    private bool _lapInvalidated;
    private bool _hasSamples;
    private int _persistenceRevision;
    private PersonalBestLap _lastCompletedValidLap;
    private int _completedValidLapRevision;
    private PendingCompletedLap? _pendingCompletedLap;
    private int _recentSectorIndex = -1;
    private double _recentSectorTimeSeconds;
    private double _recentSectorReferenceSeconds;
    private long _recentSectorExpiresAtTimestamp;

    public int PersistenceRevision => _persistenceRevision;
    public int CompletedValidLapRevision => _completedValidLapRevision;
    public PersonalBestLap LastCompletedValidLap => _lastCompletedValidLap;

    public SectorReferenceSeed PersistentReferences => new(
        _persistentReferences[0],
        _persistentReferences[1],
        _persistentReferences[2]);





}
