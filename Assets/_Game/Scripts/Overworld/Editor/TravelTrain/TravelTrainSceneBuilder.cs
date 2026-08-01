using System;

public static class TravelTrainSceneBuilder
{
    public static void BuildOrUpdate(TravelTrainDataBundle data)
    {
        if (data?.Core?.TrainRoom == null)
            throw new ArgumentNullException(nameof(data));

        GeneratedRegionSceneBuilder.Build(
            TravelTrainPaths.Scene,
            data.Core.TrainRoom,
            new[] { data.Core.TrainRoom },
            includeBattleHost: false);
    }
}