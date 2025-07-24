using UnityEngine;

public class Graph : MonoBehaviour {

	[SerializeField]
	Transform pointPrefab;

    [SerializeField, Range(1,50)]
	float resolution;

    Vector3 position;
    Vector3 scale;

    void Awake() {
        position = Vector3.zero;
        scale = Vector3.one / resolution;
    }

    void Update() {
        float time = Time.realtimeSinceStartup;
        for (float i = -10f*resolution; i < 10f*resolution; i++)
        {
            Transform Tpoint = Instantiate(pointPrefab);
            Tpoint.SetParent(transform, false);
            Tpoint.localScale = scale;

            position.x = i/resolution;
            position.y = Mathf.Sin(position.x+time);
            Tpoint.localPosition = position;
        }
    }
}

