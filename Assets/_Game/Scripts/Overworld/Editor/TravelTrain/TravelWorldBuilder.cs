using System;
using UnityEditor;
using UnityEngine;

public static class TravelWorldBuilder
{
    [MenuItem("HubToHome/Build Travel World")]
    private static void BuildFromAsciiMenu()
    {
        BuildOrUpdate();
    }

    [MenuItem("HubToHome/오버월드/본편 월드/열차와 WideField 생성-갱신")]
    public static TravelWorldBuildResult BuildOrUpdate()
    {
        if (!TravelWorldBuildPreflight.ValidateNoDirtyOwnedContent(out string preflightError))
            throw new InvalidOperationException(preflightError);

        TravelTrainPromotionResult promotion =
            TravelTrainAssetPromotion.PromoteCoreAssets(TravelTrainPromotionPaths.Default);
        if (!promotion.Success)
            throw new InvalidOperationException("열차 자산 승격 실패: " + promotion.Error);

        TravelTrainCoreAssetBundle core =
            TravelTrainContentBuilder.EnsureCoreRoomAssets(promotion);
        ShowcaseStationDataBundle showcase = ShowcaseStationDataBuilder.Build();
        WideFieldDataBundle wideField = WideFieldBuilder.BuildData();
        TravelTrainDataBundle train = TravelTrainContentBuilder.Build(
            core,
            showcase.Rooms[ShowcaseStationIds.Train],
            wideField.StationRoom);
        SaveGeneratedOwnedSceneChanges();

        TravelTrainRoomBuilder.EnsureRoom(core, train, promotion.Status);
        ShowcaseStationRoomBuilder.Build(showcase, train);
        WideFieldBuilder.BuildRooms(wideField, train);
        SaveGeneratedOwnedSceneChanges();

        ShowcaseStationSceneBuilder.BuildOrUpdate(showcase);
        TravelTrainSceneBuilder.BuildOrUpdate(train);
        WideFieldBuilder.BuildScene(wideField);
        TravelTrainEditorAssetUtility.EnsureBuildSettingsEntriesInOrder(
            ShowcaseStationScenePaths.MainScene,
            TravelTrainPaths.Scene,
            WideFieldPaths.Scene);
        AssetDatabase.SaveAssets();

        TrainTravelValidationReport validation =
            TrainTravelContentValidator.Validate(train.Network);
        ShowcaseStationValidationReport showcaseValidation =
            ShowcaseStationValidator.ValidateGeneratedAssets();
        if (validation.HasErrors || !showcaseValidation.IsValid)
        {
            throw new InvalidOperationException(
                "Travel World validation failed. TrainErrors=" + validation.ErrorCount
                + ", ShowcaseErrors=" + showcaseValidation.Errors.Count);
        }

        if (!TravelTrainAssetPromotion.RemoveLegacySceneAfterValidation(
                TravelTrainPromotionPaths.Default,
                out string removalError))
        {
            throw new InvalidOperationException(removalError);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(WideFieldPaths.Scene);
        Selection.activeObject = scene;
        if (scene != null)
            EditorGUIUtility.PingObject(scene);

        var result = new TravelWorldBuildResult
        {
            Showcase = showcase,
            WideField = wideField,
            Train = train,
            Validation = validation
        };
        Debug.Log(
            "[TravelWorldBuilder] 공용 열차와 WideField 왕복 콘텐츠 생성 완료. "
            + "Train=" + TravelTrainPaths.Scene
            + ", WideField=" + WideFieldPaths.Scene,
            scene);
        return result;
    }

    private static void SaveGeneratedOwnedSceneChanges()
    {
        if (!TravelWorldBuildPreflight.SaveGeneratedChangesInOwnedScenes(out string error))
            throw new InvalidOperationException(error);
    }
}