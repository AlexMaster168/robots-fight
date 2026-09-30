using UnityEngine;

namespace RobotsFight
{
    /// <summary>Камера от третьего лица из-за плеча + зум на ПКМ. Считает точку прицела лучом из центра экрана.</summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera I;

        public Camera Cam { get; private set; }
        public Robot Target;
        public float Yaw, Pitch = 8f;
        public float Sensitivity = 2.5f;
        public Vector3 AimPoint { get; private set; }
        public bool Zoomed { get; private set; }

        public Vector3 FlatForward => Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
        public Vector3 FlatRight => Quaternion.Euler(0, Yaw, 0) * Vector3.right;

        Vector3 spectatePivot;
        float spectateAngle;

        void Awake()
        {
            I = this;
            Cam = GetComponent<Camera>();
            Cam.nearClipPlane = 0.2f;
            Cam.farClipPlane = 900f;
        }

        public void SnapTo(Robot r)
        {
            Target = r;
            Yaw = r.transform.eulerAngles.y;
            Pitch = 8f;
        }

        public void ShowOverview()
        {
            Target = null;
            spectatePivot = Vector3.zero;
        }

        void LateUpdate()
        {
            bool alive = Target && Target.Alive;
            bool autopilot = Match.I && Match.I.Autopilot;
            bool canLook = alive && !GameRoot.InputBlocked && !autopilot;
            if (alive && autopilot)
            {
                // Демо/автопилот: камера плавно смотрит туда же, куда целится робот
                Vector3 to = Vec.Flat(Target.AimPoint - Target.transform.position);
                if (to.sqrMagnitude > 1f)
                    Yaw = Mathf.LerpAngle(Yaw, Quaternion.LookRotation(to).eulerAngles.y, 1f - Mathf.Exp(-2.5f * Time.unscaledDeltaTime));
                Pitch = Mathf.Lerp(Pitch, 10f, Time.unscaledDeltaTime);
            }
            if (canLook)
            {
                Vector2 look = GameInput.Look * Sensitivity * (Cam.fieldOfView / 60f);
                Yaw += look.x;
                Pitch = Mathf.Clamp(Pitch - look.y, -35f, 60f);
            }

            Zoomed = canLook && GameInput.Zoom;
            float zoomFov = alive && Target.Def.Weapons[0].Model == GunModel.Rail ? 18f : 35f;
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, Zoomed ? zoomFov : 60f, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));

            Vector3 pivot;
            if (alive)
            {
                float s = Target.Def.Scale;
                pivot = Target.transform.position + Vector3.up * (Target.Height + 0.9f * s);
                Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0);
                float dist = (Zoomed ? 4f : 9f) * s;
                Vector3 want = pivot + rot * new Vector3(1.7f * s, 0.3f * s, -dist);
                Vector3 dir = want - pivot;
                if (Phys.SphereCast(pivot, 0.3f, dir.normalized, dir.magnitude, Target.Cc, out var hit))
                    want = pivot + dir.normalized * Mathf.Max(0.5f, hit.distance - 0.1f);
                transform.SetPositionAndRotation(want, rot);
                spectatePivot = Target.transform.position;
            }
            else
            {
                // Наблюдение: медленный облёт точки гибели / центра карты
                pivot = spectatePivot;
                spectateAngle += 12f * Time.unscaledDeltaTime;
                Vector3 off = Quaternion.Euler(0, spectateAngle, 0) * new Vector3(0, 22f, -30f);
                transform.position = Vector3.Lerp(transform.position, pivot + off, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
                transform.rotation = Quaternion.LookRotation(pivot + Vector3.up * 2f - transform.position);
            }

            // Точка прицела: луч из центра экрана, начиная от уровня робота (чтобы не цеплять то, что между камерой и роботом)
            Vector3 fwd = transform.forward;
            float skip = Mathf.Max(0f, Vector3.Dot(pivot - transform.position, fwd));
            Vector3 origin = transform.position + fwd * skip;
            AimPoint = Phys.Raycast(origin, fwd, 800f, alive ? Target.Cc : null, out var aimHit)
                ? aimHit.point
                : origin + fwd * 800f;
        }
    }
}
