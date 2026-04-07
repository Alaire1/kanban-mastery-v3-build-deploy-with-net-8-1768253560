import { useEffect, useMemo, useState } from 'react';

export default function useColumnsPagination(columns, columnsPerView = 4) {
  const [startIndex, setStartIndex] = useState(0);

  useEffect(() => {
    setStartIndex(0);
  }, [columns.length]);

  const maxStartIndex = Math.max(0, columns.length - columnsPerView);

  const visibleColumns = useMemo(
    () => columns.slice(startIndex, startIndex + columnsPerView),
    [columns, startIndex, columnsPerView]
  );

  const canGoPrev = startIndex > 0;
  const canGoNext = startIndex < maxStartIndex;

  const handlePrevColumns = () => {
    setStartIndex((prev) => Math.max(0, prev - columnsPerView));
  };

  const handleNextColumns = () => {
    setStartIndex((prev) => Math.min(maxStartIndex, prev + columnsPerView));
  };

  return {
    visibleColumns,
    canGoPrev,
    canGoNext,
    handlePrevColumns,
    handleNextColumns,
  };
}
