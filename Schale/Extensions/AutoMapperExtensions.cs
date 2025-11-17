using AutoMapper;
using AutoMapper.QueryableExtensions;

namespace Schale.Extensions
{
    public static class AutoMapperExtensions
    {
        public static TDest? FirstOrDefaultMapTo<TSource, TDest>(this IQueryable<TSource> source, IMapper mapper)
        {
            return source.ProjectTo<TDest>(mapper.ConfigurationProvider).FirstOrDefault();
        }

        public static TDest? FirstOrDefaultMapTo<TSource, TDest>(this IQueryable<TSource> source)
        {
            return source.FirstOrDefault() is TSource item ? (TDest?)(object)item : default;
        }

        public static TDest FirstMapTo<TSource, TDest>(this IQueryable<TSource> source, IMapper mapper)
        {
            return source.ProjectTo<TDest>(mapper.ConfigurationProvider).First();
        }
    }
}
