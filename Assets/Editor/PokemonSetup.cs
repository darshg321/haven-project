using HavenDuel.Pokemon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds the roster asset from Assets/Models/Pokemon/*.glb and a showcase scene.
public static class PokemonSetup
{
    const string RosterPath = "Assets/Data/PokemonRoster.asset";
    const string ScenePath = "Assets/Scenes/ModelShowcase.unity";

    // species, file, height (m), yaw, hover, run speed. Heights follow the hitbox sizes in GAME.md.
    static readonly (Species s, string file, float h, float yaw, float hover, float run)[] Table =
    {
        (Species.Pikachu,   "pikachu",   0.6f, 0f, 0f,   5f),
        (Species.Greninja,  "greninja",  1.5f, 0f, 0f,   6f),
        (Species.Charizard, "charizard", 2.2f, 0f, 0f,   3f),
        (Species.Bulbasaur, "bulbasaur", 0.8f, 0f, 0f,   3f),
        (Species.Eevee,     "eevee",     0.55f, 0f, 0f,  5f),
        (Species.Mimikyu,   "mimikyu",   0.55f, 0f, 0.25f, 99f),
        (Species.Ditto,     "ditto",     0.4f, 0f, 0f,   99f),
    };

    [MenuItem("HavenDuel/Pokemon/Rebuild Roster Asset")]
    public static PokemonRoster BuildRoster()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        var roster = AssetDatabase.LoadAssetAtPath<PokemonRoster>(RosterPath);
        if (!roster) { roster = ScriptableObject.CreateInstance<PokemonRoster>(); AssetDatabase.CreateAsset(roster, RosterPath); }
        var list = new System.Collections.Generic.List<PokemonRoster.Entry>();
        foreach (var t in Table)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Pokemon/{t.file}.glb");
            if (!go) Debug.LogError($"Missing model Assets/Models/Pokemon/{t.file}.glb");
            list.Add(new PokemonRoster.Entry { species = t.s, model = go, height = t.h, yawOffset = t.yaw, hover = t.hover, runSpeed = t.run });
        }
        roster.entries = list.ToArray();
        EditorUtility.SetDirty(roster);
        AssetDatabase.SaveAssets();
        return roster;
    }

    [MenuItem("HavenDuel/Pokemon/Build Model Showcase Scene")]
    public static void BuildShowcase()
    {
        var roster = BuildRoster();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var cam = Camera.main;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 1.4f, -6.5f), Quaternion.Euler(8f, 0f, 0f));
        cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 2f);
        var show = new GameObject("ModelShowcase").AddComponent<ModelShowcase>();
        show.roster = roster;
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Built " + ScenePath);
    }
}
