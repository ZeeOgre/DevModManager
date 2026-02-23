#ifndef NVTRISTRIP_WRAPPER_H
#define NVTRISTRIP_WRAPPER_H

#include <QList>
#include <QVector>


class Triangle;

QVector<QVector<quint16> > stripify( const QVector<Triangle> & triangles, bool stitch = true );
QVector<Triangle> triangulate( const QVector<quint16> & strips );
QVector<Triangle> triangulate( const QVector< QVector<quint16> > & strips );

#endif
