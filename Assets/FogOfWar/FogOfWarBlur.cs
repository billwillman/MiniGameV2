using UnityEngine;

namespace FoW
{
    public enum FogOfWarBlurType
    {
        /// <summary>
        /// Skip blurring.
        /// </summary>
        None = -1,

        /// <summary>
        /// 3x3 Gaussian Blur.
        /// </summary>
        Gaussian3,

        /// <summary>
        /// 5x5 Gaussian Blur.
        /// </summary>
        Gaussian5,

        /// <summary>
        /// 3x3 Gaussian Blur only on pixels that are on an edge.
        /// </summary>
        Antialias
    }

    public class FogOfWarBlur
    {
        RenderTexture _target;
        RenderTexture _source;
        Material _blurMaterial = null;

        void SetupRenderTarget(Vector2Int resolution, ref RenderTexture tex)
        {
            if (tex == null)
                tex = new RenderTexture(resolution.x, resolution.y, 0);
            else if (tex.width != resolution.x || tex.height != resolution.y)
            {
                tex.width = resolution.x;
                tex.height = resolution.y;
            }
        }

        public void Release()
        {
            if (_target != null)
            {
                _target.Release();
                _target = null;
            }
            if (_source != null)
            {
                _source.Release();
                _source = null;
            }
            if (_blurMaterial != null)
            {
                Object.Destroy(_blurMaterial);
                _blurMaterial = null;
            }
        }

        public Texture Apply(Texture fogtexture, Vector2Int resolution, FogOfWarBlurType type, int iterations)
        {
            if (type == FogOfWarBlurType.None)
                return fogtexture;

            iterations = Mathf.Max(iterations, 1);

            if (_blurMaterial == null)
                _blurMaterial = new Material(FogOfWarUtils.FindShader("Hidden/FogOfWarBlurShader"));

            _blurMaterial.SetKeywordEnabled("GAUSSIAN3", type == FogOfWarBlurType.Gaussian3);
            _blurMaterial.SetKeywordEnabled("GAUSSIAN5", type == FogOfWarBlurType.Gaussian5);
            _blurMaterial.SetKeywordEnabled("ANTIALIAS", type == FogOfWarBlurType.Antialias);

            SetupRenderTarget(resolution, ref _target);
            if (iterations > 1)
                SetupRenderTarget(resolution, ref _source);

#if !UNITY_2021_1_OR_NEWER
            _target.MarkRestoreExpected();
#endif
            Graphics.Blit(fogtexture, _target, _blurMaterial);

            for (int i = 1; i < iterations; ++i)
            {
                FogOfWarUtils.Swap(ref _target, ref _source);
#if !UNITY_2021_1_OR_NEWER
                _target.MarkRestoreExpected();
#endif
                Graphics.Blit(_source, _target, _blurMaterial);
            }

            return _target;
        }
    }
}
