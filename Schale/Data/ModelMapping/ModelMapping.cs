using AutoMapper;

namespace Schale.Data.ModelMapping
{
    public static class ModelMapping
    {
        public static TDestination? MapInternalSingle<TSource, TDestination>(
            this TSource? sourceObject,
            IMapper mapper)
            where TSource : class
            where TDestination : class
        {
            return sourceObject == null ? null : mapper.Map<TDestination>(sourceObject);
        }

        public static IEnumerable<TDestination> MapInternalEnumerable<TSource, TDestination>(
            this IEnumerable<TSource>? sourceCollection,
            IMapper mapper)
            where TSource : class
            where TDestination : class
        {
            return sourceCollection == null ? [] : mapper.Map<IEnumerable<TDestination>>(sourceCollection);
        }

        public static List<TDestination> MapInternalList<TSource, TDestination>(this IEnumerable<TSource>? source, IMapper mapper)
        {
            return source == null ? [] : mapper.Map<List<TDestination>>(source);
        }
    }
}


